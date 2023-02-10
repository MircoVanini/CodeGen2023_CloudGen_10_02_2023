
namespace Proxima.Nano.Demo
{
    using System;
    using System.Collections;
    using System.Threading;
    using System.Text;
    using Microsoft.Extensions.Logging;
    
    public class SyncQueue
    {
        #region Delegate

        public delegate bool DequeueCallback(object arg);

        #endregion

        #region Fields

        private Thread              threadQueue        = null;
        private Queue               queue              = null;
        private AutoResetEvent      syncDequeue        = null;
        private ManualResetEvent    syncClose          = null;
        private ILogger             logger             = null;      

        #endregion

        #region Properties

        public int Count
        {
            get
            {
                lock (queue.SyncRoot)
                {
                    return (queue.Count);
                }
            }
        }

        public bool IsRunning { get; private set; }

        public int ThreadPollingTime { get; set; } = 100;

        public DequeueCallback DequeueDelegate { get; private set; }

        #endregion

        #region Constructors

        public SyncQueue(ILogger logger, DequeueCallback dequeueDelegate)
        {
            queue       = new Queue();
            syncDequeue = new AutoResetEvent(false);

            this.logger          = logger;
            this.DequeueDelegate = dequeueDelegate;
        }

        #endregion

        #region Public Methods

        public void Clear()
        {
            lock (queue.SyncRoot)
            {
                queue.Clear();
            }
        }

        public void Enqueue(object obj)
        {
            lock (queue.SyncRoot)
            {
                queue.Enqueue(obj);
            }
            syncDequeue.Set();
        }

        public object Dequeue()
        {
            lock (queue.SyncRoot)
            {
                return queue.Dequeue();
            }
        }

        public object Peek()
        {
            lock (queue.SyncRoot)
            {
                return queue.Peek();
            }
        }

        public void StartQueueThread()
        {
            try
            {
                if (threadQueue != null)
                {
                    StopQueueThread();
                    threadQueue = null;
                }

                IsRunning       = true;
                threadQueue     = new Thread(QueueThreadProc);
                syncClose       = new ManualResetEvent(false);
                
                threadQueue.Start();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(StartQueueThread)} failed !");
            }
        }

        public void StopQueueThread()
        {
            try
            {
                if (threadQueue == null)
                    return;

                IsRunning = false;
                Thread.Sleep(0);
                syncClose.WaitOne(1000, true);

                if (threadQueue.IsAlive)
                    threadQueue.Abort();

                threadQueue = null;
            }
            catch (ThreadAbortException ex)
            {
                logger.LogError(ex, $"{nameof(StartQueueThread)} failed !"); ;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(StartQueueThread)} failed !");
            }
        }

        #endregion

        #region Private Methods

        private void QueueThreadProc()
        {
            bool signal = false;

            do
            {
                signal = syncDequeue.WaitOne(ThreadPollingTime, false);

                if (IsRunning == false)
                    break;

                if (!signal)
                    continue;

                while (Count > 0)
                {
                    if (IsRunning == false)
                        break;

                    object tmp = Peek();                        

                    try
                    {
                        if (DequeueDelegate != null)
                        {
                            if (DequeueDelegate(tmp))
                                Dequeue();
                        }
                        else
                            Dequeue();
                    }
                    catch(Exception ex) 
                    {
                        logger.LogError(ex, "Call dequeueCallback failed !");
                    }

                    Thread.Sleep(100);
                }

            } while (IsRunning);

            syncClose.Set();
        }

        #endregion
    }
}
