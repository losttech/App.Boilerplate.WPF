namespace LostTech.App {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.AppCenter.Crashes;
    class WpfWarningsService: IWarningsService {
        public void Warn(Exception exception, string userFriendlyMessage, IReadOnlyDictionary<string, object?>? properties = null)
            => Crashes.TrackError(exception,
                properties?.ToDictionary(
                    keySelector: kv => kv.Key,
                    elementSelector: kv => kv.Value?.ToString() ?? "<null>")
            );

        public static void Initialize() => WarningsService.Default = new WpfWarningsService();
    }
}
