﻿namespace LostTech.App
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Windows;
    using global::Windows.System;

    /// <summary>
    /// Interaction logic for LicenseTermsAcceptance.xaml
    /// </summary>
    partial class LicenseTermsAcceptance : Window
    {
        public LicenseTermsAcceptance()
        {
            this.InitializeComponent();

            Stream resource = GetTermsAndConditions();
            this.LicenseContent.NavigateToStream(resource);
            this.LicenseContent.Navigated += delegate {
                this.LicenseContent.Navigating += (_, args) => {
                    args.Cancel = true;
                    if (!"http".Equals(args.Uri.Scheme, StringComparison.InvariantCultureIgnoreCase)
                        && !"https".Equals(args.Uri.Scheme, StringComparison.InvariantCultureIgnoreCase))
                        return;
                    if (new DesktopBridge.Helpers().IsRunningAsUwp())
                        Launcher.LaunchUriAsync(args.Uri).GetAwaiter();
                    else {
                        var startInfo = new ProcessStartInfo(args.Uri.AbsoluteUri) {
                            UseShellExecute = true,
                        };
                        Process.Start(startInfo);
                    }
                };
            };
        }

        static Stream GetTermsAndConditions()
        {
            string @namespace = typeof(LicenseTermsAcceptance).Namespace!;
            string resourceName = new DesktopBridge.Helpers().IsRunningAsUwp() ? "StoreTerms" : "Terms";
            resourceName = $"{@namespace}.{resourceName}.html";
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        }

        public static string? GetTermsAndConditionsVersion()
        {
            byte[] hash;
            using (var algorithm = SHA256.Create()) {
                Stream termsAndConditions = GetTermsAndConditions();
                if (termsAndConditions == null)
                    return null;
                hash = algorithm.ComputeHash(termsAndConditions);
            }

            return Convert.ToBase64String(hash);
        }

        void AcceptClick(object sender, RoutedEventArgs e) {
            e.Handled = true;
            this.DialogResult = true;
        }

        void DeclineClick(object sender, RoutedEventArgs e) {
            e.Handled = true;
            this.DialogResult = false;
        }
    }
}
