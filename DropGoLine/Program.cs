namespace DropGoLine {
  internal static class Program {
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main() {
      // Create a unique Mutex name. "Global\" prefix ensures it works across different user sessions if needed, 
      // though for a per-user app simply "DropGoLineApp" is often enough. 
      // Using a GUID ensures uniqueness.
      const string mutexName = "Global\\DropGoLine_App_Mutex_989d2666-d4f4-436a-8616-566b6c836936";
      
      bool createdNew;
      using (var mutex = new System.Threading.Mutex(true, mutexName, out createdNew)) {
        if (!createdNew) {
          // Verify if another instance is already running using this mutex
          // If so, exit the application immediately
          return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
      }
    }
  }
}