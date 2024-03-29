﻿#pragma warning disable RCS1090 // Call 'ConfigureAwait(false)'. - always true in GUI code
namespace LostTech.App {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;

    using JetBrains.Annotations;

    using LostTech.App.UWP;

    using Microsoft.AppCenter;
    using Microsoft.AppCenter.Analytics;
    using Microsoft.AppCenter.Crashes;
    using Microsoft.Toolkit.Uwp.Notifications;

    using Windows.Data.Xml.Dom;
    using Windows.UI.Notifications;

    using static System.FormattableString;

    public abstract class BoilerplateApp : Application, INotifyPropertyChanged, IAsyncDisposable {
        internal static readonly bool IsUwp = new DesktopBridge.Helpers().IsRunningAsUwp();

        readonly DirectoryInfo localSettingsFolder;
        readonly DirectoryInfo roamingSettingsFolder;

        Settings? localSettings;
        Settings? roamingSettings;
        Task<StartupResult>? startupCompletion;
        BoilerplateSettings? settings;

        protected BoilerplateApp() {
            this.localSettingsFolder = new DirectoryInfo(this.AppDataDirectory.FullName);
            this.roamingSettingsFolder = new DirectoryInfo(this.RoamingAppDataDirectory.FullName);
        }

        public new static BoilerplateApp Current => (BoilerplateApp)Application.Current;
        public static BoilerplateApp Boilerplate => (BoilerplateApp)Application.Current;
        public abstract string AppName { get; }
        public abstract string CompanyName { get; }
        public abstract TimeSpan HeartbeatInterval { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected abstract WhatsNew? WhatsNew { get; }

        protected virtual Task<StartupResult> StartupCompletion => this.startupCompletion ?? throw new InvalidOperationException();
        protected Settings LocalSettings => this.localSettings ?? throw new InvalidOperationException();
        protected Settings RoamingSettings => this.roamingSettings ?? throw new InvalidOperationException();

        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            WpfWarningsService.Initialize();

            this.startupCompletion = this.StartupImpl();
        }

        async Task<StartupResult> StartupImpl() {
            var startupResult = new StartupResult();

            // TODO: let user opt-in/-out
            this.EnableTelemetry();

            this.localSettings = XmlSettings.Create(this.localSettingsFolder);
            this.roamingSettings = XmlSettings.Create(this.roamingSettingsFolder);

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
                startupResult.TermsUpdated = true;
            }

            string version = this.Version is null
                ? "N/A"
                : Invariant($"{this.Version.Major}.{this.Version.Minor}");
            if (this.settings.WhatsNewVersionSeen != version && this.WhatsNew != null) {
                this.ShowNotification(title: this.WhatsNew.Title,
                    message: this.WhatsNew.Message,
                    navigateTo: this.WhatsNew.DetailsUri);
            }

            this.settings.WhatsNewVersionSeen = version;

            return startupResult;
        }

        protected async Task<T> InitializeSettingsSet<T>(string fileName)
            where T : class, new() {
            SettingsSet<T, T> settingsSet;
            try {
                settingsSet = await this.localSettings!.LoadOrCreate<T>(fileName);
            } catch (Exception settingsError) {
                string errorFile = Path.Combine(this.localSettingsFolder.FullName, $"{fileName}.err");
                File.Create(errorFile).Close();
                Debug.WriteLine(settingsError.ToString());
                File.WriteAllText(path: errorFile, contents: settingsError.ToString());
                string brokenFile = Path.Combine(this.localSettingsFolder.FullName, fileName);
                string brokenBackup = Path.Combine(this.localSettingsFolder.FullName, $"Err.{fileName}");
                File.Copy(brokenFile, destFileName: brokenBackup, overwrite: true);
                File.Delete(brokenFile);
                settingsSet = await this.localSettings!.LoadOrCreate<T>(fileName);
                settingsSet.ScheduleSave();
            }
            settingsSet.Autosave = true;
            return settingsSet.Value;
        }

        protected abstract string? AppCenterSecret { get; }
        void EnableTelemetry() {
            string? hockeyID = this.AppCenterSecret;
            if (string.IsNullOrEmpty(hockeyID))
                return;

            AppCenter.Start(hockeyID, typeof(Crashes), typeof(Analytics));

            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        }

        static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
            foreach (Exception exception in e.Exception.Flatten().InnerExceptions)
                Crashes.TrackError(exception,
                    properties: new Dictionary<string, string> { ["unobserved"] = "true" });
        }

        static void EnableJitDebugging() {
            AppDomain.CurrentDomain.UnhandledException += (_, args) => Debugger.Launch();
        }

        static bool modernNotificationsUnavailable;
        public void ShowNotification(string? title, string message, Uri? navigateTo, TimeSpan? duration = null) {
            if (modernNotificationsUnavailable) {
                var buttons = navigateTo is null ? MessageBoxButton.OK : MessageBoxButton.OKCancel;
                if (MessageBox.Show(messageBoxText: message, caption: title,
                                    buttons, MessageBoxImage.Information) == MessageBoxResult.OK
                    && navigateTo is not null) {
                    this.Launch(navigateTo);
                }
                return;
            }

            try {
                ShowModernNotification(title, message, navigateTo, duration);
            } catch (TypeLoadException) {
                modernNotificationsUnavailable = true;
                this.ShowNotification(title, message, navigateTo, duration);
            }
        }

        // can't inline because of crashes on Windows before 10
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ShowModernNotification(string? title, string message, Uri? navigateTo, TimeSpan? duration = null) {
            var header = title == null ? null : new ToastHeader(title, title, navigateTo?.ToString());
            var content = new ToastContent {
                Launch = navigateTo?.ToString(),

                Header = header,

                Visual = new ToastVisual {
                    BindingGeneric = new ToastBindingGeneric {
                        Children = { new AdaptiveText { Text = message } },
                    }
                }
            };

            if (navigateTo is not null) {
                content.ActivationType = ToastActivationType.Protocol;
                if (header is not null)
                    header.ActivationType = ToastActivationType.Protocol;
            }

            var contentXml = new XmlDocument();
            contentXml.LoadXml(content.GetContent());
            var toast = new ToastNotification(contentXml) {
                // DTO + null == null
                ExpirationTime = DateTimeOffset.Now + duration,
            };
            try {
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
            } catch (Exception e) {
                WarningsService.Default.ReportAsWarning(e, prefix: $"Notification failed");
            }
        }

        public virtual async ValueTask DisposeAsync() {
            Settings?[] settingsToDispose = { this.localSettings, this.roamingSettings };
            await Task.WhenAll(settingsToDispose.Select(async setToDispose => {
                if (setToDispose is null) return;
                setToDispose.ScheduleSave();
                await setToDispose.DisposeAsync().ConfigureAwait(false);
            }));
        }

        public async void BeginShutdown() {
            Debug.WriteLine("shutdown requested");

            await this.DisposeAsync();

            this.Shutdown();
        }

        internal static Assembly GetResourceContainer() => Assembly.GetExecutingAssembly();

        public DirectoryInfo AppDataDirectory {
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

        public DirectoryInfo RoamingAppDataDirectory {
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

        public void Launch(Uri uri) {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (!uri.IsAbsoluteUri) throw new ArgumentException(message: "Must be absolute URI", paramName: nameof(uri));

            var startInfo = new ProcessStartInfo(uri.AbsoluteUri) {
                UseShellExecute = true,
            };
            Process.Start(startInfo);
        }

        // can't inline because of crashes on Windows before 10
        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetUwpRoamingAppData() => global::Windows.Storage.ApplicationData.Current.RoamingFolder.Path;

        public Version? Version => IsUwp
            ? GetUwpVersion()
            : Assembly.GetEntryAssembly()?.GetName().Version;
        [MethodImpl(MethodImplOptions.NoInlining)]
        static Version GetUwpVersion() => global::Windows.ApplicationModel.Package.Current.Id.Version.ToVersion();

        public static void StartNewInstance() { Process.Start(GetExecutablePath()); }

        public static void StartNewInstanceAsAdmin() {
            var startInfo = new ProcessStartInfo(GetExecutablePath()) {
                Verb = "runas",
                UseShellExecute = true,
            };
            Process.Start(startInfo);
        }

        static string GetExecutablePath() {
            return Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new NotSupportedException("Unable to get executable path");
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
