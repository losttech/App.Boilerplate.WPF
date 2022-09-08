namespace LostTech.App.WPF;

using System.Collections.Generic;
using System.Linq;

public sealed class WpfAppDependency {
    public static IEnumerable<AppDependecy> Dependencies { get; } =
        AppDependecy.Dependencies.Concat(
            new[] {
                new AppDependecy("Lost Tech App Boilerplate", uri: "https://github.com/losttech/App.Boilerplate", license: "Apache 2.0"),
                new AppDependecy($"Lost Tech {nameof(XmlSettings)}", uri: "https://github.com/losttech/App.XmlSettings", license: "Apache 2.0"),
                new AppDependecy(nameof(DesktopBridge), uri: "https://github.com/microsoft/Windows-AppConsult-Tools-DesktopBridgeHelpers", license: "MIT"),
                new AppDependecy("Windows Community Toolkit", uri: "https://github.com/CommunityToolkit/WindowsCommunityToolkit", license: "MIT"),
                new AppDependecy("Visual Studio App Center", uri: "https://azure.microsoft.com/en-us/services/app-center/", license: "MIT"),
            });
}
