using System;
using System.Threading;
using System.IO;

namespace TPProj
{
    class Program
    {
        public static double Factorial(int number)
        {
            if (number < 0) return -1;
            if (number == 0) return 1;
            double result = 1;
            for (int i = 1; i <= number; i++)
            {
                result *= i;
            }
            return result;
        }
        public static double CalculateP0(double rho, int n)
        {
            if (rho < 0 || n < 0) return -1;

            double sum = 0;
            for (int i = 0; i <= n; i++)
            {
                sum += Math.Pow(rho, i) / Factorial(i);
            }
            if (sum == 0) return -1;
            return 1.0 / sum;
        }
        public static double CalculateFailureProbability(double rho, int n, double p0)
        {
             if (rho < 0 || n < 0 || p0 < 0) return -1;
            return (Math.Pow(rho, n) / Factorial(n)) * p0;
        }

        static void Main()
        {
            int numberOfChannels = 5;
            int totalRequests = 100;
            int serverProcessingTime = 500;
            int sampleIntervalMs = 20;

            int[] clientSendIntervals = { 25, 33, 40, 50, 67, 80, 100, 125, 167, 250, 500 };
            string csvFilePath = "simulation_results.csv";

            Console.WriteLine($"Запуск серии экспериментов для построения графиков...");
            Console.WriteLine($"Результаты будут сохранены в файл: {Path.GetFullPath(csvFilePath)}");

            try
            {
                using (StreamWriter sw = new StreamWriter(csvFilePath))
                {
                    sw.WriteLine("clientSendIntervalMs;Lambda;P0_Эксп;P_Отк_Эксп;Q_Эксп;A_Эксп;k_Эксп;P0_Теор;P_Отк_Теор;Q_Теор;A_Теор;k_Теор");

                    foreach (int currentClientSendInterval in clientSendIntervals)
                    {
                        Console.WriteLine($"Обработка эксперимента для clientSendInterval = {currentClientSendInterval} мс...");

                        double lambda = (currentClientSendInterval > 0) ? 1000.0 / currentClientSendInterval : double.PositiveInfinity;
                        double mu = (serverProcessingTime > 0) ? 1000.0 / serverProcessingTime : double.PositiveInfinity;

                        Server server = new Server(numberOfChannels, serverProcessingTime);
                        Client client = new Client(server);

                        long idleStateObservations = 0;
                        long totalStateObservations = 0;

                        for (int id = 1; id <= totalRequests; id++)
                        {
                            client.send(id);
                            if (currentClientSendInterval > 0)
                            {
                                int elapsedInInterval = 0;
                                while (elapsedInInterval < currentClientSendInterval)
                                {
                                    int sleepDuration = Math.Min(sampleIntervalMs, currentClientSendInterval - elapsedInInterval);
                                    Thread.Sleep(sleepDuration);
                                    elapsedInInterval += sleepDuration;

                                    totalStateObservations++;
                                    if (server.ActiveChannels == 0)
                                    {
                                        idleStateObservations++;
                                    }
                                }
                            }
                            else
                            {
                                totalStateObservations++;
                                if (server.ActiveChannels == 0)
                                {
                                    idleStateObservations++;
                                }
                            }
                        }

                        int finalWaitMs = 1000;
                        int elapsedInFinalWait = 0;
                        while (elapsedInFinalWait < finalWaitMs)
                        {
                            int sleepDuration = Math.Min(sampleIntervalMs, finalWaitMs - elapsedInFinalWait);
                            Thread.Sleep(sleepDuration);
                            elapsedInFinalWait += sleepDuration;

                            totalStateObservations++;
                            if (server.ActiveChannels == 0)
                            {
                                idleStateObservations++;
                            }
                        }
                        
                        double p0Exp = 0, failureProbExp = 0, relativeThroughputExp = 0, absoluteThroughputExp = 0, avgNumOccupiedChannelsExp = 0;
                        if (server.requestCount > 0 && totalStateObservations > 0)
                        {
                            p0Exp = (double)idleStateObservations / totalStateObservations;
                            failureProbExp = (double)server.rejectedCount / server.requestCount;
                            relativeThroughputExp = (double)server.processedCount / server.requestCount;
                            absoluteThroughputExp = lambda * relativeThroughputExp; 
                            avgNumOccupiedChannelsExp = relativeThroughputExp * (lambda / mu); 
                        }

                        double p0_teor = 0, failureProbTeor = 0, relativeThroughputTeor = 0, absoluteThroughputTeor = 0, avgNumOccupiedChannelsTeor = 0;
                        double rho = -1;
                        if (mu > 0)
                        {
                            rho = lambda / mu;
                            if (rho >= 0)
                            {
                                p0_teor = CalculateP0(rho, numberOfChannels);
                                if (p0_teor >= 0)
                                {
                                    failureProbTeor = CalculateFailureProbability(rho, numberOfChannels, p0_teor);
                                    relativeThroughputTeor = 1.0 - failureProbTeor;
                                    absoluteThroughputTeor = lambda * relativeThroughputTeor;
                                    avgNumOccupiedChannelsTeor = rho * relativeThroughputTeor;
                                }
                            }
                        }

                        string csvLine = $"{currentClientSendInterval};{lambda:F4};{p0Exp:F4};{failureProbExp:F4};{relativeThroughputExp:F4};{absoluteThroughputExp:F4};{avgNumOccupiedChannelsExp:F4};{p0_teor:F4};{failureProbTeor:F4};{relativeThroughputTeor:F4};{absoluteThroughputTeor:F4};{avgNumOccupiedChannelsTeor:F4}".Replace(",", ".");
                        sw.WriteLine(csvLine);
                        
                    }
                    Console.WriteLine("\nДанные успешно записаны в файл.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при записи в CSV файл: {ex.Message}");
            }
            
            Console.WriteLine("\nСерия экспериментов завершена");
        }
    }

    struct PoolRecord
    {
        public Thread thread;
        public bool in_use;
    }

    class Server
    {
        private PoolRecord[] pool;
        private object threadLock = new object();
        public int requestCount = 0;
        public int processedCount = 0;
        public int rejectedCount = 0;
        private int poolSize;
        private int processingTime;
        public int ActiveChannels { get; private set; } = 0;

        public Server(int n, int procTime)
        {
            poolSize = n;
            processingTime = procTime;
            pool = new PoolRecord[poolSize];
            ActiveChannels = 0;
        }
        public void proc(object sender, procEventArgs e)
        {
            lock (threadLock)
            {
                // Console.WriteLine("Заявка с номером: {0}", e.id);
                requestCount++;
                for (int i = 0; i < poolSize; i++)
                {
                    if (!pool[i].in_use)
                    {
                        pool[i].in_use = true;
                        ActiveChannels++;
                        pool[i].thread = new Thread(new ParameterizedThreadStart(Answer));
                        pool[i].thread.Start(new Tuple<int, int, int>(e.id, i, processingTime));
                        processedCount++;
                        Console.WriteLine("Заявка {0} принята на обработку потоком {1}", e.id, i);
                        return;
                    }
                }
                rejectedCount++;
                Console.WriteLine("Заявка {0} отклонена, нет свободных потоков", e.id);
            }
        }

        public void Answer(object arg)
        {
            Tuple<int, int, int> data = (Tuple<int, int, int>)arg;
            int requestId = data.Item1;
            int poolIndex = data.Item2;
            int currentProcessingTime = data.Item3;

            Console.WriteLine("Поток {0} начал обработку заявки: {1} (время: {2} мс)", poolIndex, requestId, currentProcessingTime);
            if (currentProcessingTime > 0)
            {
                Thread.Sleep(currentProcessingTime);
            }
            Console.WriteLine("Поток {0} завершил обработку заявки: {1}", poolIndex, requestId);

            lock(threadLock)
            {
                pool[poolIndex].in_use = false;
                ActiveChannels--;
                // Console.WriteLine("Поток {0} освободился", poolIndex);
            }
        }
    }

    class Client
    {
        private Server server;

        public Client(Server server)
        {
            this.server = server;
            this.request += server.proc;
        }

        public void send(int id)
        {
            procEventArgs args = new procEventArgs();
            args.id = id;
            OnProc(args);
        }

        protected virtual void OnProc(procEventArgs e)
        {
            EventHandler<procEventArgs> handler = request;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler<procEventArgs> request;
    }

    public class procEventArgs : EventArgs
    {
        public int id { get; set; }
    }
}
