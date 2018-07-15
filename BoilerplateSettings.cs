namespace LostTech.App {
    using LostTech.App.DataBinding;

    public class BoilerplateSettings : NotifyPropertyChangedBase, ICopyable<BoilerplateSettings> {
        string acceptedTerms = null;
        string whatsNewVersionSeen = null;

        public string AcceptedTerms {
            get => this.acceptedTerms;
            set {
                this.acceptedTerms = value;
                this.OnPropertyChanged();
            }
        }

        public string WhatsNewVersionSeen {
            get => this.whatsNewVersionSeen;
            set {
                this.whatsNewVersionSeen = value;
                this.OnPropertyChanged();
            }
        }

        public BoilerplateSettings Copy() => new BoilerplateSettings {
            AcceptedTerms = this.AcceptedTerms,
            WhatsNewVersionSeen = this.WhatsNewVersionSeen,
        };
    }
}
