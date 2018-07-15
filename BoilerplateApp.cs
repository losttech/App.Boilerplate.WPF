namespace LostTech.App {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;
    using JetBrains.Annotations;
    using LostTech.App.UWP;
    using Microsoft.HockeyApp;
    using Microsoft.Toolkit.Uwp.Notifications;

    using Windows.Data.Xml.Dom;
    using Windows.UI.Notifications;

    using static System.FormattableString;

    public abstract class BoilerplateApp : Application, INotifyPropertyChanged {
        internal static readonly bool IsUwp = new DesktopBridge.Helpers().IsRunningAsUwp();

        readonly DispatcherTimer updateTimer = new DispatcherTimer(DispatcherPriority.Background) {
            Interval = TimeSpan.FromDays(1),
            IsEnabled = true,
        };
        readonly DispatcherTimer heartbeat = new DispatcherTimer(DispatcherPriority.Background) {
            Interval = TimeSpan.FromDays(14),
            IsEnabled = true,
        };

        readonly DirectoryInfo localSettingsFolder;
        readonly DirectoryInfo roamingSettingsFolder;

        Settings localSettings;
        Task<StartupResult> startupCompletion;
        BoilerplateSettings settings;

        protected BoilerplateApp() {
            this.localSettingsFolder = new DirectoryInfo(this.AppData.FullName);
            this.roamingSettingsFolder = new DirectoryInfo(this.RoamingAppData.FullName);
        }

        public new static BoilerplateApp Current => Application.Current as BoilerplateApp;
        public static BoilerplateApp Boilerplate => Application.Current as BoilerplateApp;
        public abstract string AppName { get; }
        public abstract string CompanyName { get; }
        public abstract TimeSpan HeartbeatInterval { get; }
        public bool HeartbeatEnabled {
            get => this.heartbeat.IsEnabled;
            set {
                if (this.heartbeat.IsEnabled == value)
                    return;

                this.heartbeat.IsEnabled = value;
                this.OnPropertyChanged();

                if (this.heartbeat.IsEnabled)
                    this.TelemetryHeartbeat(this, EventArgs.Empty);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected abstract WhatsNew WhatsNew { get; }

        protected virtual Task<StartupResult> StartupCompletion => this.startupCompletion ?? throw new InvalidOperationException();
        protected Settings LocalSettings { get => this.localSettings ?? throw new InvalidOperationException(); }

        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            this.startupCompletion = this.StartupImpl(e);
        }

        async Task<StartupResult> StartupImpl(StartupEventArgs e) {
            var startupResult = new StartupResult();

            await this.EnableHockeyApp();

            if (!IsUwp) {
                this.BeginCheckForUpdates();
                this.updateTimer.Tick += (_, __) => this.BeginCheckForUpdates();
            }

            this.localSettings = XmlSettings.Create(this.localSettingsFolder);
            this.settings = await this.InitializeSettingsSet<BoilerplateSettings>("App.Boilerplate.xml");

            bool termsVersionMismatch = this.settings.AcceptedTerms != LicenseTermsAcceptance.GetTermsAndConditionsVersion();
            if (termsVersionMismatch) {
                var termsWindow = new LicenseTermsAcceptance();
                if (!true.Equals(termsWindow.ShowDialog())) {
                    this.Shutdown();
                    startupResult.LaunchCancelled = true;
                    return startupResult;
                }
                termsWindow.Close();
                this.settings.AcceptedTerms = LicenseTermsAcceptance.GetTermsAndConditionsVersion();
            }

            string version = Invariant($"{this.Version.Major}.{this.Version.Minor}");
            if (this.settings.WhatsNewVersionSeen != version && this.WhatsNew != null) {
                this.ShowNotification(title: this.WhatsNew.Title,
                    message: this.WhatsNew.Message,
                    navigateTo: this.WhatsNew.DetailsUri);
            }

            this.settings.WhatsNewVersionSeen = version;

            return startupResult;
        }

        async Task<T> InitializeSettingsSet<T>(string fileName)
            where T : class, new() {
            SettingsSet<T, T> settingsSet;
            try {
                settingsSet = await this.localSettings.LoadOrCreate<T>(fileName);
            } catch (Exception settingsError) {
                string errorFile = Path.Combine(this.localSettingsFolder.FullName, $"{fileName}.err");
                File.Create(errorFile).Close();
                Debug.WriteLine(settingsError.ToString());
                File.WriteAllText(path: errorFile, contents: settingsError.ToString());
                string brokenFile = Path.Combine(this.localSettingsFolder.FullName, fileName);
                string brokenBackup = Path.Combine(this.localSettingsFolder.FullName, $"Err.{fileName}");
                File.Copy(brokenFile, destFileName: brokenBackup, overwrite: true);
                File.Delete(brokenFile);
                settingsSet = await this.localSettings.LoadOrCreate<T>(fileName);
                settingsSet.ScheduleSave();
            }
            settingsSet.Autosave = true;
            return settingsSet.Value;
        }

        void BeginCheckForUpdates() {
            HockeyClient.Current.CheckForUpdatesAsync(autoShowUi: true, shutdownActions: () => {
                this.BeginShutdown();
                return true;
            }).GetAwaiter();
        }

        readonly DateTimeOffset bootTime = DateTimeOffset.UtcNow;

        TimeSpan Uptime => DateTimeOffset.UtcNow - this.bootTime;

        protected abstract string HockeyAppID { get; }
        async Task EnableHockeyApp() {
            string hockeyID = this.HockeyAppID;
            if (string.IsNullOrEmpty(hockeyID))
                return;

            HockeyClient.Current.Configure(hockeyID);
            ((HockeyClient)HockeyClient.Current).OnHockeySDKInternalException += (sender, args) => {
                if (Debugger.IsAttached) { Debugger.Break(); }
            };

            try {
                await HockeyClient.Current.SendCrashesAsync().ConfigureAwait(false);
            } catch (IOException e) when ((e.HResult ^ unchecked((int)0x8007_0000)) == (int)Win32ErrorCode.ERROR_NO_MORE_FILES) { }

            this.heartbeat.Interval = this.HeartbeatInterval;
            this.heartbeat.Tick += this.TelemetryHeartbeat;
            if (this.heartbeat.IsEnabled)
                this.TelemetryHeartbeat(this.heartbeat, EventArgs.Empty);

            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        }

        static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e) {
            foreach (Exception exception in e.Exception.Flatten().InnerExceptions)
                HockeyClient.Current.TrackException(exception,
                    properties: new Dictionary<string, string> { ["unobserved"] = "true" });
        }

        void TelemetryHeartbeat(object sender, EventArgs e) {
            var eventData = new Dictionary<string, string> {
                [nameof(this.HeartbeatInterval)] = Invariant($"{this.HeartbeatInterval.TotalMinutes}"),
                [nameof(this.Version)] = Invariant($"{this.Version}"),
                [nameof(this.Uptime)] = Invariant($"{this.Uptime}"),
                [nameof(IsUwp)] = Invariant($"{IsUwp}"),
            };
            this.FillHeartbeatData(eventData);
            HockeyClient.Current.TrackEvent("Heartbeat", eventData);
        }

        protected virtual void FillHeartbeatData(IDictionary<string, string> eventData) { }

        static void EnableJitDebugging() {
            AppDomain.CurrentDomain.UnhandledException += (_, args) => Debugger.Launch();
        }

        // can't inline because of crashes on Windows before 10
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ShowNotification(string title, string message, Uri navigateTo, TimeSpan? duration = null) {
            var content = new ToastContent {
                Launch = navigateTo.ToString(),

                Header = title == null ? null : new ToastHeader(title, title, navigateTo.ToString()),

                Visual = new ToastVisual {
                    BindingGeneric = new ToastBindingGeneric {
                        Children = { new AdaptiveText { Text = message } },
                    }
                }
            };

            var contentXml = new XmlDocument();
            contentXml.LoadXml(content.GetContent());
            var toast = new ToastNotification(contentXml) {
                // DTO + null == null
                ExpirationTime = DateTimeOffset.Now + duration,
            };
            try {
                DesktopNotificationManagerCompat.CreateToastNotifier().Show(toast);
            } catch (Exception e) {
                e.ReportAsWarning(prefix: $"Notification failed");
            }
        }

        protected virtual async Task DisposeAsync() {
            Settings settings = this.localSettings;
            if (settings != null) {
                settings.ScheduleSave();
                await settings.DisposeAsync();
                this.localSettings = null;
                Debug.WriteLine("settings written");
            }
        }

        public async void BeginShutdown() {
            Debug.WriteLine("shutdown requested");

            await this.DisposeAsync();

            this.Shutdown();
        }

        protected override void OnExit(ExitEventArgs exitArgs) {
            base.OnExit(exitArgs);
            HockeyClient.Current.Flush();
            Thread.Sleep(1000);
        }

        internal static Assembly GetResourceContainer() => Assembly.GetExecutingAssembly();

        public DirectoryInfo AppData {
            get {
                string path;
                if (IsUwp) {
                    path = GetUwpAppData();
                } else {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    path = Path.Combine(appData, this.CompanyName, this.AppName);
                }
                return Directory.CreateDirectory(path);
            }
        }

        // can't inline because of crashes on Windows before 10
        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetUwpAppData() => global::Windows.Storage.ApplicationData.Current.LocalFolder.Path;

        public DirectoryInfo RoamingAppData {
            get {
                string path;
                if (IsUwp) {
                    path = GetUwpRoamingAppData();
                } else {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    path = Path.Combine(appData, this.CompanyName, this.AppName);
                }
                return Directory.CreateDirectory(path);
            }
        }

        // can't inline because of crashes on Windows before 10
        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetUwpRoamingAppData() => global::Windows.Storage.ApplicationData.Current.RoamingFolder.Path;

        public Version Version => IsUwp
            ? GetUwpVersion()
            : Assembly.GetEntryAssembly().GetName().Version;
        [MethodImpl(MethodImplOptions.NoInlining)]
        static Version GetUwpVersion() => global::Windows.ApplicationModel.Package.Current.Id.Version.ToVersion();

        public static void StartNewInstance() { Process.Start(Process.GetCurrentProcess().MainModule.FileName); }

        public static void StartNewInstanceAsAdmin() {
            var startInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName) { Verb = "runas" };
            Process.Start(startInfo);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
