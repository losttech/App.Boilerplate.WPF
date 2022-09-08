namespace LostTech.App {
    public record WhatsNew(string Title, string Message) {
        public System.Uri? DetailsUri { get; init; }
    }
}
