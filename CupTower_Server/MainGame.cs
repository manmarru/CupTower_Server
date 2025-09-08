namespace MainApp;

class MainApp
{
    static async Task Main()
    {
        Server.Server server = new Server.Server();
        server.Initialize();

        List<Thread> Threads = new List<Thread>();
        for (int i = 0; i < Server.Server.MAXUSER; ++i)
        {
            int userIndex = i;
            Thread thread = new Thread(() => server.Recv_N_Send(userIndex));
            Threads.Add(thread);
            thread.IsBackground = false;
            thread.Start();
        }


        // System.Console.WriteLine("Press Any key to End Server");
        // Console.ReadLine(); // 아무거나 입력하면 종료

        //server.Release();
    }
}