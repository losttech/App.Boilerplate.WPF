namespace LostTech.App.UWP {
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Microsoft.Toolkit.Uwp.Notifications;

    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(INotificationActivationCallback))]
    [Guid("0ED9D73E-024A-4CF4-88C7-8BEC0568B437"), ComVisible(true)]
    public class UrlNotificationActivator : NotificationActivator {
        public override void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId) {
            var uri = new Uri(arguments, UriKind.Absolute);
            var startInfo = new ProcessStartInfo(uri.ToString()) {
                UseShellExecute = true,
            };
            Process.Start(startInfo);
        }
    }
}
