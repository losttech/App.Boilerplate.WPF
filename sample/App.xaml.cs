namespace LostTech.App;

using System;

public partial class App : BoilerplateApp {
    public override string AppName => "Sample";

    public override string CompanyName => "Contoso";

    public override TimeSpan HeartbeatInterval => TimeSpan.FromDays(30);

    protected override WhatsNew? WhatsNew => new("Hi", "There") { DetailsUri = new Uri("https://example.com") };

    protected override string? AppCenterSecret => null;
}
