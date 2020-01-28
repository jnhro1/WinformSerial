using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using System.Text.RegularExpressions;


namespace WinformTest1
{
    public partial class Form1 : Form

    {
        SerialPort serialPort1 = new SerialPort();
        delegate void SetTextCallback(string opt);
        //string RBuffer; // 수신버퍼로 사용할 문자열 변수선언
        private Thread testThread;
        private byte[] cpuArray = new byte[6000];

        private Queue<int> que = new Queue<int>();
        private Queue<string> que_string = new Queue<string>();

        private Point currentMouseLcation = Point.Empty; // 현재 마우스 위치
        private RectangleF plotArea = RectangleF.Empty; // InnerPlotPostion

        Stopwatch sw = new Stopwatch(); // 경과시간 측정을 위한 stopwatch 선언

        int cnt = 0;
        long wheelZoom = 10000;
        double ZoomScale = 100;
        Boolean wheelDown = true;

        public Form1()
        {
            InitializeComponent();
            serialPort1.DataReceived += new SerialDataReceivedEventHandler(serialPort1_DataReceived);
        }

        private void update_serial_combobox()
        {
            string[] portsArray = SerialPort.GetPortNames();

            comboBox1.Items.Clear();

            foreach (string portnumber in portsArray)
            {
                comboBox1.Items.Add(portnumber);
            }
        }

        private void comboBox1_DropDown(object sender, EventArgs e)
        {
            update_serial_combobox();
        }

        private void button_PortOpen_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen == true)
            {
                serialPort1.Close();
            }

            if (comboBox1.SelectedItem == null)
            {
                return;
            }

            serialPort1.PortName = comboBox1.SelectedItem.ToString();
            serialPort1.BaudRate = Int32.Parse(comboBox2.SelectedItem.ToString());

            try
            {
                serialPort1.Open();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("포트가 이미 열려 있습니다!");
            }

            if (serialPort1.IsOpen == true)
            {
                serialPort1.DiscardInBuffer();

                button_PortOpen.Enabled = false;
                button_PortClose.Enabled = true;

                comboBox1.Enabled = false;
                comboBox2.Enabled = false;

                richTextBox2.Enabled = true;


                SerialChart1.Series[0].Points.Clear(); // 시리얼차트에 찍힌 데이터포인트를 클리어한다.
                SerialChart1.ChartAreas[0].AxisX.Minimum = 0;// sw.ElapsedMilliseconds; // 시리얼차트의 x축 최소값을 스탑워치의 현재시간값으로 설정.
                SerialChart1.ChartAreas[0].AxisX.Maximum = sw.ElapsedMilliseconds + 10000; // 시리얼차트의 x축 최대값을 스탑워치의 '현재시간 + 5000'값으로 설정.
                                                                                           // SerialChart1.ChartAreas[0].AxisX.Maximum = 50;
                SerialChart1.ChartAreas[0].AxisX.LabelStyle.Format = "#";
                SerialChart1.ChartAreas[0].AxisY.Minimum = 0;
                SerialChart1.ChartAreas[0].AxisY.Maximum = 4096;
                SerialChart1.ChartAreas[0].AxisY.LabelStyle.Format = "#";

                SerialChart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
                SerialChart1.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
                SerialChart1.MouseWheel += SerialChart1_MouseWheel;

                sw.Start(); // 정의된 스탑워치 sw를 통해 경과시간측정을 시작한다.

                testThread = new Thread(new ThreadStart(this.getPerformanceCounters));
                testThread.IsBackground = true;
                testThread.Start();

            }
        }


        private void button_PortClose_Click(object sender, EventArgs e)
        {
            serialPort1.Close();

            button_PortOpen.Enabled = true;
            button_PortClose.Enabled = false;

            comboBox1.Enabled = true;
            comboBox2.Enabled = true;

            richTextBox2.Enabled = false;
            //richTextBox4.Enabled = false;
            //richTextBox5.Enabled = false;
            // SerialChart1.Enabled = false;

            sw.Reset(); // 경과시작측정을 중지하고 경과시작을 다시 0으로 설정.
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox2.SelectedIndex = 0;

            button_PortOpen.Enabled = true;
            button_PortClose.Enabled = false;
            comboBox1.Enabled = true;
            comboBox2.Enabled = true;
            richTextBox2.Enabled = false;
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int i_recv_size = serialPort1.BytesToRead;
            byte[] b_tmp_buf = new byte[i_recv_size];
            string recv_str = "";


            serialPort1.Read(b_tmp_buf, 0, i_recv_size);


            recv_str = Encoding.Default.GetString(b_tmp_buf);

            string[] sp = new string[] { ", \r\n" };
            string[] spstring = recv_str.Split(sp, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < spstring.Length; i++)
            {
                cnt++;
                if (que.Count >= 10000)
                {
                    que.Dequeue();
                }
                que.Enqueue(int.Parse(spstring[i]));
            }


            this.BeginInvoke(new SetTextCallback(display_data), new object[] { recv_str });
            this.BeginInvoke(new SetTextCallback(str2hex), new object[] { recv_str });

        }


        private void display_data(string str)
        {
            richTextBox1.Text += str;
            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.ScrollToCaret();

        }

        private void str2hex(string strData)
        {
            string resultHex = string.Empty;
            byte[] arr_byteStr = Encoding.Default.GetBytes(strData);

            int cnt = 0;
            int linecnt = 0;

            foreach (byte byteStr in arr_byteStr)
            {
                resultHex += string.Format("{0:X2}" + " ", byteStr);
                cnt++;
                linecnt++;
                resultHex = resultHex.Replace("0A", "0A\r\n");
            }

            richTextBox3.Text += resultHex;
            richTextBox3.SelectionStart = richTextBox3.TextLength;
            richTextBox3.ScrollToCaret();

        }


        private void richTextBox2_KeyPress_1(object sender, KeyPressEventArgs e)
        {
            if (!serialPort1.IsOpen)
                return;

            char[] buffer = new char[1];
            buffer[0] = e.KeyChar;

            serialPort1.Write(buffer, 0, buffer.Length);
            richTextBox2.Text = "";
        }

        public static int GetNumberOnly(string _strInput)

        {
            string strValue = System.Text.RegularExpressions.Regex.Replace(_strInput, @"[^\d]", "");
            int nResult = -1;

            if (int.TryParse(strValue, out nResult) == false)
            {
                return nResult;
            }
            return nResult;

        }


        private void getPerformanceCounters()
        {
            while (true)
            {
                if (SerialChart1.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)delegate { UpdateCpuChart(); });
                }

                Thread.Sleep(50);
            }
        }

        private void UpdateCpuChart()
        {
            for (int k = 0; k < que.Count; k++)
            {
                if (SerialChart1.Series[0].Points.Count < sw.ElapsedMilliseconds - 10000)
                {
                    //SerialChart1.Series[0].Points.RemoveAt(0);
                    SerialChart1.ChartAreas[0].AxisX.Minimum = sw.ElapsedMilliseconds - wheelZoom;// SerialChart1.Series[0].Points[0].XValue;
                    SerialChart1.ChartAreas[0].AxisX.Maximum = sw.ElapsedMilliseconds;

                }
                int a = que.Dequeue();

                SerialChart1.Series[0].Points.AddXY(sw.ElapsedMilliseconds, a);
            }
        }

        private void SerialChart1_MouseWheel(object sender, MouseEventArgs e)
        {
            var chart = (Chart)sender;
            var xAxis = chart.ChartAreas[0].AxisX;

            try
            {
                if (e.Delta < 0) // Scrolled down. 
                {
                    if (sw.ElapsedMilliseconds - wheelZoom < 4000)
                    {
                        wheelZoom = sw.ElapsedMilliseconds;

                        if (wheelDown == true)
                        {
                            ZoomScale += 20;
                        }
                        wheelDown = false;
                    }
                    else
                    {
                        wheelZoom += 2000;
                        ZoomScale += 20;
                        wheelDown = true;
                    }

                    SerialChart1.ChartAreas[0].AxisX.Minimum = sw.ElapsedMilliseconds - wheelZoom;
                    SerialChart1.ChartAreas[0].AxisX.Maximum = sw.ElapsedMilliseconds;
                }
                else if (e.Delta > 0) // Scrolled up. 
                {
                    if (wheelZoom > 2000)
                    {
                        wheelZoom -= 2000;
                        ZoomScale -= 20;
                        wheelDown = true;
                    }

                    SerialChart1.ChartAreas[0].AxisX.Minimum = sw.ElapsedMilliseconds - wheelZoom;
                    SerialChart1.ChartAreas[0].AxisX.Maximum = sw.ElapsedMilliseconds;
                }
            }
            catch { }
        }


    }
}