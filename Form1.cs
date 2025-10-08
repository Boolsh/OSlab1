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

            var lbBuffer1 = new ListBox() { Name = "lbBuffer1", Top = 10, Left = 10, Width = 250, Height = 200 };
            var lbBuffer2 = new ListBox() { Name = "lbBuffer2", Top = 10, Left = 270, Width = 250, Height = 200 };
            this.Controls.Add(lbBuffer1);
            this.Controls.Add(lbBuffer2);

            var lbl1 = new Label() {  Top = 220, Left = 10, Width = 250 };
            var lbl2 = new Label() {  Top = 220, Left = 270, Width = 250 };
            this.Controls.Add(lbl1);
            this.Controls.Add(lbl2);

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

            var tbLog = new TextBox() { Name = "tbLog", Top = 10, Left = 540, Width = 320, Height = 480, Multiline = true, ScrollBars = ScrollBars.Vertical };
            this.Controls.Add(tbLog);

            var lblStatusProducer = new Label() { Name = "lblProducer", Top = 260, Left = 270, Width = 250 };
            var lblStatusProcessor = new Label() { Name = "lblProcessor", Top = 300, Left = 270, Width = 250 };
            var lblStatusConsumer = new Label() { Name = "lblConsumer", Top = 340, Left = 270, Width = 250 };
            this.Controls.AddRange(new Control[] { lblStatusProducer, lblStatusProcessor, lblStatusConsumer });

            var btnStop = new Button() { Text = "Stop All", Top = 380, Left = 10, Width = 250 };
            btnStop.Click += (s, e) => StopAll();
            this.Controls.Add(btnStop);

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
            producerPause.Set();
            processorPause.Set();
            consumerPause.Set();
        }

        private void ProducerWork()
        {
            while (!stopRequested)
            {
                producerPause.Wait();

                string msg = $"Msg#{Interlocked.Increment(ref producedCount)}";

                if (buffer1.Put(msg))
                    LogOnUI("Producer: " + msg);
                else
                    LogOnUI("Producer: Buffer1 full, skipping...");

                Thread.Sleep(900);
            }

            LogOnUI("Producer stopped.");
        }

        private void ProcessorWork()
        {
            while (!stopRequested)
            {
                processorPause.Wait();

                if (buffer1.Take(out string taken))
                {
                    string processed = taken + " + ProcessorTag";
                    if (buffer2.Put(processed))
                        LogOnUI("Processor: " + processed);
                    else
                        LogOnUI("Processor: Buffer2 full, skipping...");
                }
                else
                {
                    LogOnUI("Processor: Buffer1 empty, waiting...");
                }

                Thread.Sleep(1100);
            }

            LogOnUI("Processor stopped.");
        }

        private void ConsumerWork()
        {
            while (!stopRequested)
            {
                consumerPause.Wait();

                if (buffer2.Take(out string taken))
                {
                    string finalMsg = taken + " + ConsumerTag";
                    LogOnUI("Consumer: " + finalMsg);
                }
                else
                {
                    LogOnUI("Consumer: Buffer2 empty, waiting...");
                }

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

        public bool Put(T item)
        {
            lock (sync)
            {
                if (stack.Count >= capacity)
                    return false;

                stack.Push(item);
                return true;
            }
        }

        public bool Take(out T item)
        {
            lock (sync)
            {
                if (stack.Count == 0)
                {
                    item = default;
                    return false;
                }

                item = stack.Pop();
                return true;
            }
        }

        public T[] GetSnapshot()
        {
            lock (sync)
            {
                return stack.ToArray();
            }
        }
    }
}
