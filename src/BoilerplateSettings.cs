namespace LostTech.App {
    using LostTech.App.DataBinding;

    public class BoilerplateSettings : NotifyPropertyChangedBase, IBoilerplateSettings, ICopyable<BoilerplateSettings> {
        string? acceptedTerms = null;
        string? whatsNewVersionSeen = null;
        bool? reportCrashes;
        bool? enableTelemetry;

        public string? AcceptedTerms {
            get => this.acceptedTerms;
            set {
                this.acceptedTerms = value;
                this.OnPropertyChanged();
            }
        }

        public string? WhatsNewVersionSeen {
            get => this.whatsNewVersionSeen;
            set {
                this.whatsNewVersionSeen = value;
                this.OnPropertyChanged();
            }
        }

        public bool? ReportCrashes {
            get => this.reportCrashes;
            set {
                this.reportCrashes = value;
                this.OnPropertyChanged();
            }
        }

        public bool? EnableTelemetry {
            get => this.enableTelemetry;
            set {
                this.enableTelemetry = value;
                this.OnPropertyChanged();
            }
        }

        public BoilerplateSettings Copy() => new BoilerplateSettings {
            AcceptedTerms = this.AcceptedTerms,
            WhatsNewVersionSeen = this.WhatsNewVersionSeen,
            ReportCrashes = this.ReportCrashes,
            EnableTelemetry = this.EnableTelemetry,
        };
    }
}
