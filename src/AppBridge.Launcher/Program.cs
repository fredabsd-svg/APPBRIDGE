using AppBridge.Launcher;

[STAThread]
void Main()
{
    var app = new App();
    app.InitializeComponent();
    app.Run();
}
