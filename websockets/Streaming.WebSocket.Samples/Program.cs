using System;
using System.Threading;
using System.Threading.Tasks;

namespace Streaming.WebSocket.Samples
{
    /// <summary>
    /// Sample runner.
    /// </summary>
    class Program
    {
        private static Action _closeConnectionAction;
        static void Main(string[] args)
        {
            Console.WriteLine("Press ESC to disconnect.");

            Task taskKeys = new Task(ListenForEscape);

            CancellationTokenSource cts = new CancellationTokenSource();
            Task taskRunSample = new Task(async () => { await RunSample(cts); });

            taskKeys.Start();
            try
            {
                taskRunSample.Start();
            }
            catch (TaskCanceledException)
            {
                taskRunSample.Dispose();
            }

            Task.WaitAll(taskKeys);
            
            cts.Cancel();
            taskRunSample.Wait(cts.Token);
            Console.WriteLine("Press ESC to close window.");
            Console.ReadKey();
        }

        /// <summary>
        /// Run the sample and set up a callback to handle Web Socket close on ESC.
        /// </summary>
        /// <param name="cts"></param>
        private static async Task RunSample(CancellationTokenSource cts)
        {
            WebSocketSample sample = new WebSocketSample();
            async void CloseConnectionCallback() => await sample.StopWebSocket();
            _closeConnectionAction = CloseConnectionCallback;

            try
            {
                await sample.RunSample(cts);
            }
            finally
            {
                sample.Dispose();
                cts.Cancel();
            }

            Console.WriteLine("Stopped sample.");
        }

        #region Input handling
        /// <summary>
        /// Listen for keyboard input from user.
        /// </summary>
        private static void ListenForEscape()
        {
            while (!Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape)
                {
                    _closeConnectionAction?.Invoke();
                    break;
                }
            }
        }
        #endregion 
    }
}
