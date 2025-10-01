using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace OSlab1
{
    public partial class Form1 : Form
    {

        private readonly Buffer<string> buffer1 = new Buffer<string>(capacity: 5);
        private readonly Buffer<string> buffer2 = new Buffer<string>(capacity: 5);


        private Thread producerThread;
        private Thread processorThread;
        private Thread consumerThread;

        private readonly ManualResetEventSlim producerPause = new ManualResetEventSlim(true);
        private readonly ManualResetEventSlim processorPause = new ManualResetEventSlim(true);
        private readonly ManualResetEventSlim consumerPause = new ManualResetEventSlim(true);

        private volatile bool stopRequested = false;

        // Для логов
        private int producedCount = 0;

        public Form1()
        {
            InitializeComponent();
            InitUI();
            this.Load += (s, e) =>
            {
                StartThreads();
                uiUpdateTimer.Start();
            };
        }

        private void InitUI()
        {
            this.Text = "Producer-Processor-Consumer";
            this.Width = 900;
            this.Height = 600;
            //Текстбоксы
            var lbBuffer1 = new ListBox() { Name = "lbBuffer1", Top = 10, Left = 10, Width = 250, Height = 200 };
            var lbBuffer2 = new ListBox() { Name = "lbBuffer2", Top = 10, Left = 270, Width = 250, Height = 200 };
            this.Controls.Add(lbBuffer1);
            this.Controls.Add(lbBuffer2);
            var lbl1 = new Label() { Text = "Buffer1 (stack top -> index 0)", Top = 220, Left = 10, Width = 250 };
            var lbl2 = new Label() { Text = "Buffer2 (stack top -> index 0)", Top = 220, Left = 270, Width = 250 };
            this.Controls.Add(lbl1);
            this.Controls.Add(lbl2);

            // Кнопки
            var btnProd = new Button() { Text = "Pause Producer", Top = 260, Left = 10, Width = 120 };
            var btnProdResume = new Button() { Text = "Resume Producer", Top = 260, Left = 140, Width = 120 };
            btnProd.Click += (s, e) => { producerPause.Reset(); UpdateThreadStatus(); };
            btnProdResume.Click += (s, e) => { producerPause.Set(); UpdateThreadStatus(); };

            var btnProc = new Button() { Text = "Pause Processor", Top = 300, Left = 10, Width = 120 };
            var btnProcResume = new Button() { Text = "Resume Processor", Top = 300, Left = 140, Width = 120 };
            btnProc.Click += (s, e) => { processorPause.Reset(); UpdateThreadStatus(); };
            btnProcResume.Click += (s, e) => { processorPause.Set(); UpdateThreadStatus(); };

            var btnCons = new Button() { Text = "Pause Consumer", Top = 340, Left = 10, Width = 120 };
            var btnConsResume = new Button() { Text = "Resume Consumer", Top = 340, Left = 140, Width = 120 };
            btnCons.Click += (s, e) => { consumerPause.Reset(); UpdateThreadStatus(); };
            btnConsResume.Click += (s, e) => { consumerPause.Set(); UpdateThreadStatus(); };

            this.Controls.AddRange(new Control[] { btnProd, btnProdResume, btnProc, btnProcResume, btnCons, btnConsResume });

            // Log textbox
            var tbLog = new TextBox() { Name = "tbLog", Top = 10, Left = 540, Width = 320, Height = 480, Multiline = true, ScrollBars = ScrollBars.Vertical };
            this.Controls.Add(tbLog);

            // Status labels
            var lblStatusProducer = new Label() { Name = "lblProducer", Top = 260, Left = 270, Width = 250 };
            var lblStatusProcessor = new Label() { Name = "lblProcessor", Top = 300, Left = 270, Width = 250 };
            var lblStatusConsumer = new Label() { Name = "lblConsumer", Top = 340, Left = 270, Width = 250 };
            this.Controls.AddRange(new Control[] { lblStatusProducer, lblStatusProcessor, lblStatusConsumer });

            // Stop button
            var btnStop = new Button() { Text = "Stop All", Top = 380, Left = 10, Width = 250 };
            btnStop.Click += (s, e) => StopAll();
            this.Controls.Add(btnStop);

            // Timer to update UI
            uiUpdateTimer.Interval = 300; 
            uiUpdateTimer.Tick += (s, e) =>
            {
                var arr1 = buffer1.GetSnapshot();
                var arr2 = buffer2.GetSnapshot();
                var lb1 = (ListBox)this.Controls["lbBuffer1"];
                var lb2 = (ListBox)this.Controls["lbBuffer2"];
                lb1.Items.Clear();
                lb2.Items.Clear();
                foreach (var item in arr1) lb1.Items.Add(item);
                foreach (var item in arr2) lb2.Items.Add(item);

                UpdateThreadStatus();
            };
        }

        private readonly System.Windows.Forms.Timer uiUpdateTimer = new System.Windows.Forms.Timer();

        private void UpdateThreadStatus()
        {
            var lblProducer = (Label)this.Controls["lblProducer"];
            var lblProcessor = (Label)this.Controls["lblProcessor"];
            var lblConsumer = (Label)this.Controls["lblConsumer"];

            lblProducer.Text = $"Producer: {(producerThread?.IsAlive == true ? "Running" : "Stopped")}, {(producerPause.IsSet ? "Active" : "Paused")}";
            lblProcessor.Text = $"Processor: {(processorThread?.IsAlive == true ? "Running" : "Stopped")}, {(processorPause.IsSet ? "Active" : "Paused")}";
            lblConsumer.Text = $"Consumer: {(consumerThread?.IsAlive == true ? "Running" : "Stopped")}, {(consumerPause.IsSet ? "Active" : "Paused")}";
        }

        private void StartThreads()
        {
            stopRequested = false;

            producerThread = new Thread(ProducerWork) { IsBackground = true, Name = "Producer" };
            processorThread = new Thread(ProcessorWork) { IsBackground = true, Name = "Processor" };
            consumerThread = new Thread(ConsumerWork) { IsBackground = true, Name = "Consumer" };

            producerThread.Start();
            processorThread.Start();
            consumerThread.Start();
        }

        private void StopAll()
        {
            stopRequested = true;
            // Resume all so they can exit if paused
            producerPause.Set();
            processorPause.Set();
            consumerPause.Set();

            // Pulse buffers in case threads are waiting
            buffer1.PulseAll();
            buffer2.PulseAll();
        }

        private void ProducerWork()
        {
            while (!stopRequested)
            {
                producerPause.Wait(); // pause/resume

                // produce message
                string msg = $"Msg#{Interlocked.Increment(ref producedCount)}";

                // попытка поместить в buffer1 (блокирующая, если полный)
                buffer1.Put(msg);

                LogOnUI("Producer: " + msg);

                // small delay to visualize
                Thread.Sleep(900);
            }

            LogOnUI("Producer stopped.");
        }

        private void ProcessorWork()
        {
            while (!stopRequested)
            {
                processorPause.Wait();

                // take from buffer1 (блокирующая, если пуст)
                string taken = buffer1.Take();

                // add its info
                string processed = taken + " + ProcessorTag";

                // put into buffer2
                buffer2.Put(processed);

                LogOnUI("Processor: " + processed);

                Thread.Sleep(1100);
            }

            LogOnUI("Processor stopped.");
        }

        private void ConsumerWork()
        {
            while (!stopRequested)
            {
                consumerPause.Wait();

                string taken = buffer2.Take();
               

                string finalMsg = taken + " + ConsumerTag";

                LogOnUI("Consumer: " + finalMsg);

                Thread.Sleep(1300);
            }

            LogOnUI("Consumer stopped.");
        }

        private void LogOnUI(string text)
        {
            this.BeginInvoke(new Action(() =>
            {
                var tb = (TextBox)this.Controls["tbLog"];
                tb.AppendText(text + Environment.NewLine);
                tb.ScrollToCaret();
            }));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopAll();
            base.OnFormClosing(e);
        }
    }

    // Буфер: стек с монитором синхронизации (Enter/Exit, Wait/Pulse)
    public class Buffer<T>
    {
        private readonly Stack<T> stack = new Stack<T>();
        private readonly int capacity;
        private readonly object sync = new object();

        public Buffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        // Положить элемент (блокирует, если полный)
        public void Put(T item)
        {
            bool lockTaken = false;
            try
            {
                Monitor.Enter(sync, ref lockTaken);
                while (stack.Count >= capacity)
                {
                    // ждать освобождения места
                    Monitor.Wait(sync);
                }

                stack.Push(item);

                // уведомляем потребителей
                Monitor.PulseAll(sync);
            }
            finally
            {
                if (lockTaken) Monitor.Exit(sync);
            }
        }

        // Взять элемент (блокирует, если пуст)
        public T Take()
        {
            bool lockTaken = false;
            try
            {
                Monitor.Enter(sync, ref lockTaken);
                while (stack.Count == 0)
                {
                    // ждать появления элемента
                    Monitor.Wait(sync);
                }

                T item = stack.Pop();
                // уведомляем производителей (что есть место)
                Monitor.PulseAll(sync);
                return item;
            }
            finally
            {
                if (lockTaken) Monitor.Exit(sync);
            }
        }

        // Получить копию содержимого (для UI) — с захватом монитора
        public T[] GetSnapshot()
        {
            bool lockTaken = false;
            try
            {
                Monitor.Enter(sync, ref lockTaken);
                return stack.ToArray(); // ToArray возвращает массив с вершины стека первым
            }
            finally
            {
                if (lockTaken) Monitor.Exit(sync);
            }
        }

        // Само по себе не стандартный API, но полезно, чтобы "разбудить" все ожидающие потоки при завершении
        public void PulseAll()
        {
            bool lockTaken = false;
            try
            {
                Monitor.Enter(sync, ref lockTaken);
                Monitor.PulseAll(sync);
            }
            finally
            {
                if (lockTaken) Monitor.Exit(sync);
            }
        }
    }
}
