﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Ports;
using System.Threading;
using Windows.Foundation;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.System;
using Windows.Devices.SerialCommunication;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Windows.Storage;
using System.Diagnostics;

namespace DepthFunnelingForUWP
{
    /// <summary>
    /// MainWindow_UWP에 대한 상호 작용 논리
    /// </summary>
    public sealed partial class MainPage : Page
    {
        SerialPort port;

        int timeInterval = 100, activationTime = 30, distanceDivision = 20;
        double distanceInterval = 2.0 / 20;
        double rootAmp = 0, middleAmp = 0, tipAmp = 0;

        bool timeFeedbackActivated = false;
        bool timeFeedbackOccured = false;
        Timer timer;

        bool distanceFeedbackActivated = false;
        double _p = 0;

        bool continuousFeedbackActivated = false;

        bool feedback = false;

        int currentIndex = -1;
        int actuatorNum, settingNum, fingerNum;
        IList<int> order;

        //System.IO.StreamWriter file;
        StorageFile file;

        // for TCP connection to Unity
        //string serverIP = "10.0.1.7";    // IP address of local computer (ipconfig)
        string serverIP = "10.0.1.16";  // IP address of Hololens
        string serverPort = "50000";



        public MainPage()
        {
            actuatorNum = 2;
            settingNum = 3;
            fingerNum = 7;

            ForArduinoManagement();

            initExperiment();
            InitializeComponent();
            initParams();

            var timerState = new TimerState { Counter = 0 };
            timer = new Timer(timerTask, timerState, timeInterval, timeInterval);

            // UWP Client (for connection to Unity)
            ConnectToHololens();

            
        }

        private async void ConnectToHololens()
        {
            /*
            try
            {
                while (true)
                {
                    // Create the StreamSocket and establish a connection to the echo server.
                    using (var streamSocket = new Windows.Networking.Sockets.StreamSocket())
                    {
                        // The server hostname that we will be establishing a connection to. In this example, the server and client are in the same process.
                        var hostName = new Windows.Networking.HostName(serverIP);

                        await streamSocket.ConnectAsync(hostName, serverPort);

                        Debug.WriteLine("client connected");

                        Debug.WriteLine("bf read");
                        string response;
                        using (Stream inputStream = streamSocket.InputStream.AsStreamForRead()) 
                        {
                            Debug.WriteLine("2", inputStream.ToString());
                            try
                            {
                                using (StreamReader streamReader = new StreamReader(inputStream))
                                {
                                    Debug.WriteLine("3");
                                    response = await streamReader.ReadLineAsync();
                                }

                                Debug.WriteLine("The data is " + response);

                                double data;

                                data = Double.Parse(response);

                                feedbackPointSlider.Value = data;
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine("The process failed: {0}", e.ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("The process failed: {0}", ex.ToString());
            }
            */

            try
            {
                var streamSocketListener = new Windows.Networking.Sockets.StreamSocketListener();

                // The ConnectionReceived event is raised when connections are received.
                streamSocketListener.ConnectionReceived += this.StreamSocketListener_ConnectionReceived;


                // Start listening for incoming TCP connections on the specified port. You can specify any port that's not currently in use.
                await streamSocketListener.BindServiceNameAsync(serverPort);
            }
            catch (Exception ex)
            {
                Windows.Networking.Sockets.SocketErrorStatus webErrorStatus = Windows.Networking.Sockets.SocketError.GetStatus(ex.GetBaseException().HResult);
            }
        }

        private async void StreamSocketListener_ConnectionReceived(Windows.Networking.Sockets.StreamSocketListener sender, Windows.Networking.Sockets.StreamSocketListenerConnectionReceivedEventArgs args)
        {
            string request;
            using (var streamReader = new StreamReader(args.Socket.InputStream.AsStreamForRead()))
            {
                request = await streamReader.ReadLineAsync();

                Debug.WriteLine("The data is " + request);

                double data;
                if (request != null)
                {
                    data = Double.Parse(request);

                    if (data >= 0 && data <= 2)
                    {
                        this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            if (feedbackCheckBox.IsChecked == false) feedbackCheckBox.IsChecked = true;
                        });
                        this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            feedbackPointSlider.Value = data;
                        });
                    }
                    else
                    {
                        this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            feedbackCheckBox.IsChecked = false;
                        });
                    }
                }



            }
        }

        private async void ForArduinoManagement()
        {
            /*  string deviceSelector = SerialDevice.GetDeviceSelectorFromUsbVidPid(2341, 0042);    // It doesn't work...
            Console.WriteLine(deviceSelector);

            Console.WriteLine("HereHereHere");
            //string selector = SerialDevice.GetDeviceSelector("COM3");
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(deviceSelector);

            Console.WriteLine("What's the problem?");
            if (devices.Count > 0)
            {
                DeviceInformation deviceInfo = devices[0];

                SerialDevice serialDevice = await SerialDevice.FromIdAsync(deviceInfo.Id);

                portName = "COM3";
            }   */
            string portName = null;
            
            portName = "COM3";  // need to be changed according to USB port
            port = new SerialPort(portName, 115200);
            port.Open();
        }

        void initExperiment()
        {
            FileWriting();

            order = Enumerable.Range(0, actuatorNum * settingNum * fingerNum).ToList();
            Shuffle(order);
            currentIndex = -1;
        }

        private async void FileWriting ()
        {
            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            IReadOnlyList<StorageFile> fileList = await storageFolder.GetFilesAsync();

            int fileno = 1;

            foreach (StorageFile onefile in fileList)   // 불안한 부분 : foreach를 사용했을 때, fileno에 맞추어 정렬된 순서로 검사가 이루어지면 성공, 그렇지 않으면 불완전한 코드
            {
                if (onefile.Name == "result" + fileno.ToString() + ".csv")
                {
                    fileno++;
                }
            }
            
            file = await storageFolder.CreateFileAsync("result" + fileno + ".csv", CreationCollisionOption.ReplaceExisting);

            string i = "Actuator, Setting, Target, Value";

            WriteOneLine(i);
        }

        private async void WriteOneLine(string i)
        {
            var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            // if (stream != null) stream.Dispose();   // 기존의 streamwriter file이 null이면 file.Close() 해준 부분
            using (var outputStream = stream.GetOutputStreamAt(0))
            {
                using (var dataWriter = new DataWriter(outputStream))
                {
                    dataWriter.WriteString(i);

                    await dataWriter.StoreAsync();  // store text into file
                    await outputStream.FlushAsync();    // close stream
                }
            }
            stream.Dispose(); // Or use the stream variable (see previous code snippet) with a using statement as well.
        }

        private void timerTask(object timerState)
        {
            if (timeFeedbackActivated)
            {
#if TYPE_OUTPUT
				Console.WriteLine("time feedback");
#endif
                timeFeedbackOccured = true;
                activateActuator();
            }
        }

        private void feedbackTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            feedbackStatusChanged();
        }

        private void feedbackCheckBoxChanged(object sender, Windows.UI.Xaml.RoutedEventArgs e)  //Windows.UI.Xaml.RoutedEventArgs 맞나..?
        {
            feedbackStatusChanged();
        }

        private void feedbackStatusChanged()
        {
            if (feedbackCheckBox == null || feedbackTypeComboBox == null)
                return;

            feedback = feedbackCheckBox.IsChecked == true;
            int selected = feedbackTypeComboBox.SelectedIndex;

            if (feedback == true)
            {
                switch (selected)
                {
                    case 0:
                        timeFeedbackActivated = true;
                        distanceFeedbackActivated = false;
                        continuousFeedbackActivated = false;
                        break;
                    case 1:
                        timeFeedbackActivated = false;
                        distanceFeedbackActivated = true;
                        continuousFeedbackActivated = false;
                        break;
                    case 2:
                        timeFeedbackActivated = true;
                        distanceFeedbackActivated = true;
                        continuousFeedbackActivated = false;
                        break;
                    case 3:
                        timeFeedbackActivated = false;
                        distanceFeedbackActivated = false;
                        continuousFeedbackActivated = true;
                        break;
                    default:
                        throw new Exception("feedback type does not exist!");
                }
            }

            else
            {
                timeFeedbackActivated = false;
                distanceFeedbackActivated = false;
                continuousFeedbackActivated = false;
            }
        }

        private void functionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (constantSlider == null)
                return;

            int selected = (sender as ComboBox).SelectedIndex;

            switch (selected)
            {
                case 0:
                    constantSlider.Minimum = 0;
                    constantSlider.Maximum = 4;
                    constantSlider.Value = 1;
                    constantSlider.TickFrequency = 0.01;
                    break;
                case 1:
                    constantSlider.Minimum = 1;
                    constantSlider.Maximum = 100;
                    constantSlider.Value = 2;
                    constantSlider.TickFrequency = 1;
                    break;
                case 2:
                    constantSlider.Minimum = 1;
                    constantSlider.Maximum = 100;
                    constantSlider.Value = 2;
                    constantSlider.TickFrequency = 1;
                    break;
                case 3:
                    constantSlider.Minimum = 0;
                    constantSlider.Maximum = Math.PI / 2.0;
                    constantSlider.Value = 1;
                    constantSlider.TickFrequency = 0.01;
                    break;
                default:
                    throw new Exception("function does not exist!");
            }

            drawGraph();
        }
        private void constantValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            drawGraph();
        }


        private void actuatorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            drawGraph();
        }


        private void feedbackPointValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (functionComboBox == null || constantSlider == null)
                return;

            int function = functionComboBox.SelectedIndex;
            double c = constantSlider.Value;
            double p = (sender as Slider).Value;
            double f = 0;

            double rootCurrentAmp = 0, middleCurrentAmp = 0, tipCurrentAmp = 0;

            if (actuatorComboBox.SelectedIndex == 0)
            {
                f = p / 2;

                switch (function)
                {
                    case 0:
                        rootCurrentAmp = Math.Pow(1 - f, c);
                        tipCurrentAmp = Math.Pow(f, c);
                        break;
                    case 1:
                        rootCurrentAmp = (Math.Pow(c, 1 - f) - 1) / (c - 1);
                        tipCurrentAmp = (Math.Pow(c, f) - 1) / (c - 1);
                        break;
                    case 2:
                        rootCurrentAmp = Math.Log(1 + (c - 1) * (1 - f)) / Math.Log(c);
                        tipCurrentAmp = Math.Log(1 + (c - 1) * f) / Math.Log(c);
                        break;
                    case 3:
                        rootCurrentAmp = Math.Tan(2 * ((1 - f) - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                        tipCurrentAmp = Math.Tan(2 * (f - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                        break;
                    default:
                        throw new Exception("function does not exist!");
                }
            }

            else
            {
                switch (function)
                {
                    case 0:
                        if (p < 1)
                        {
                            f = p;
                            rootCurrentAmp = Math.Pow(1 - f, c);
                            middleCurrentAmp = Math.Pow(f, c);
                        }
                        else
                        {
                            f = p - 1;
                            middleCurrentAmp = Math.Pow(1 - f, c);
                            tipCurrentAmp = Math.Pow(f, c);
                        }
                        break;
                    case 1:
                        if (p < 1)
                        {
                            f = p;
                            rootCurrentAmp = (Math.Pow(c, 1 - f) - 1) / (c - 1);
                            middleCurrentAmp = (Math.Pow(c, f) - 1) / (c - 1);
                        }
                        else
                        {
                            f = p - 1;
                            middleCurrentAmp = (Math.Pow(c, 1 - f) - 1) / (c - 1);
                            tipCurrentAmp = (Math.Pow(c, f) - 1) / (c - 1);
                        }
                        break;
                    case 2:
                        if (p < 1)
                        {
                            f = p;
                            rootCurrentAmp = Math.Log(1 + (c - 1) * (1 - f)) / Math.Log(c);
                            middleCurrentAmp = Math.Log(1 + (c - 1) * f) / Math.Log(c);
                        }
                        else
                        {
                            f = p - 1;
                            middleCurrentAmp = Math.Log(1 + (c - 1) * (1 - f)) / Math.Log(c);
                            tipCurrentAmp = Math.Log(1 + (c - 1) * f) / Math.Log(c);
                        }
                        break;
                    case 3:
                        if (p < 1)
                        {
                            f = p;
                            rootCurrentAmp = Math.Tan(2 * ((1 - f) - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                            middleCurrentAmp = Math.Tan(2 * (f - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                        }
                        else
                        {
                            f = p - 1;
                            middleCurrentAmp = Math.Tan(2 * ((1 - f) - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                            tipCurrentAmp = Math.Tan(2 * (f - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                        }
                        break;
                    default:
                        throw new Exception("function does not exist!");
                }
            }

            rootCurrentAmplitudeSlider.Value = rootCurrentAmp;
            middleCurrentAmplitudeSlider.Value = middleCurrentAmp;
            tipCurrentAmplitudeSlider.Value = tipCurrentAmp;

            rootAmp = rootMaxAmplitudeSlider.Value * rootCurrentAmp;
            middleAmp = middleMaxAmplitudeSlider.Value * middleCurrentAmp;
            tipAmp = tipMaxAmplitudeSlider.Value * tipCurrentAmp;

            if (timeFeedbackOccured)
            {
                timeFeedbackOccured = false;
                _p = p;
            }

            if (distanceFeedbackActivated && Math.Abs(p - _p) >= distanceInterval)
            {
                _p = p;
#if TYPE_OUTPUT
				Console.WriteLine("distance feedback");
#endif
                timer.Change(timeInterval, timeInterval);

                activateActuator();
            }
        }

        private void activateActuator()
        {
#if CONSOLE_OUTPUT
			Console.WriteLine("1a" + Math.Round(rootAmp, 3));
			Console.WriteLine("2a" + Math.Round(middleAmp, 3));
			Console.WriteLine("3a" + Math.Round(tipAmp, 3));
#endif
            port.WriteLine("1a" + Math.Round(rootAmp, 3));
            port.WriteLine("2a" + Math.Round(middleAmp, 3));
            port.WriteLine("3a" + Math.Round(tipAmp, 3));

#if CONSOLE_OUTPUT
			Console.WriteLine("r");
#endif
            port.WriteLine("r");
            Thread.Sleep(activationTime);
#if CONSOLE_OUTPUT
			Console.WriteLine("s");
#endif
            port.WriteLine("s");
        }

        private void drawGraph()
        {
            if (graphCanvas == null || functionComboBox == null || constantSlider == null)
                return;

            double width = graphCanvas.Width;
            double height = graphCanvas.Height;

            PointCollection rootPoints = new PointCollection();
            PointCollection middlePoints = new PointCollection();
            PointCollection tipPoints = new PointCollection();

            for (int i = 0; i <= 200; i++)
            {
                int function = functionComboBox.SelectedIndex;
                double c = constantSlider.Value;
                double p = i / 100.0;
                double f = 0;

                double rootCurrentAmp = 0, middleCurrentAmp = 0, tipCurrentAmp = 0;

                if (actuatorComboBox.SelectedIndex == 0)
                {
                    f = p / 2;

                    switch (function)
                    {
                        case 0:
                            rootCurrentAmp = Math.Pow(1 - f, c);
                            tipCurrentAmp = Math.Pow(f, c);
                            break;
                        case 1:
                            rootCurrentAmp = (Math.Pow(c, 1 - f) - 1) / (c - 1);
                            tipCurrentAmp = (Math.Pow(c, f) - 1) / (c - 1);
                            break;
                        case 2:
                            rootCurrentAmp = Math.Log(1 + (c - 1) * (1 - f)) / Math.Log(c);
                            tipCurrentAmp = Math.Log(1 + (c - 1) * f) / Math.Log(c);
                            break;
                        case 3:
                            rootCurrentAmp = Math.Tan(2 * ((1 - f) - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                            tipCurrentAmp = Math.Tan(2 * (f - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                            break;
                        default:
                            throw new Exception("function does not exist!");
                    }
                }

                else
                {
                    switch (function)
                    {
                        case 0:
                            if (p < 1)
                            {
                                f = p;
                                rootCurrentAmp = Math.Pow(1 - f, c);
                                middleCurrentAmp = Math.Pow(f, c);
                            }
                            else
                            {
                                f = p - 1;
                                middleCurrentAmp = Math.Pow(1 - f, c);
                                tipCurrentAmp = Math.Pow(f, c);
                            }
                            break;
                        case 1:
                            if (p < 1)
                            {
                                f = p;
                                rootCurrentAmp = (Math.Pow(c, 1 - f) - 1) / (c - 1);
                                middleCurrentAmp = (Math.Pow(c, f) - 1) / (c - 1);
                            }
                            else
                            {
                                f = p - 1;
                                middleCurrentAmp = (Math.Pow(c, 1 - f) - 1) / (c - 1);
                                tipCurrentAmp = (Math.Pow(c, f) - 1) / (c - 1);
                            }
                            break;
                        case 2:
                            if (p < 1)
                            {
                                f = p;
                                rootCurrentAmp = Math.Log(1 + (c - 1) * (1 - f)) / Math.Log(c);
                                middleCurrentAmp = Math.Log(1 + (c - 1) * f) / Math.Log(c);
                            }
                            else
                            {
                                f = p - 1;
                                middleCurrentAmp = Math.Log(1 + (c - 1) * (1 - f)) / Math.Log(c);
                                tipCurrentAmp = Math.Log(1 + (c - 1) * f) / Math.Log(c);
                            }
                            break;
                        case 3:
                            if (p < 1)
                            {
                                f = p;
                                rootCurrentAmp = Math.Tan(2 * ((1 - f) - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                                middleCurrentAmp = Math.Tan(2 * (f - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                            }
                            else
                            {
                                f = p - 1;
                                middleCurrentAmp = Math.Tan(2 * ((1 - f) - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                                tipCurrentAmp = Math.Tan(2 * (f - 0.5) * c) / (2 * Math.Tan(c)) + 0.5;
                            }
                            break;
                        default:
                            throw new Exception("function does not exist!");
                    }
                }

                rootPoints.Add(new Point(p / 2 * width, (1 - rootCurrentAmp) * height));
                middlePoints.Add(new Point(p / 2 * width, (1 - middleCurrentAmp) * height));
                tipPoints.Add(new Point(p / 2 * width, (1 - tipCurrentAmp) * height));
            }

            Polyline rootPolyline = new Polyline();
            rootPolyline.StrokeThickness = 1;
            rootPolyline.Stroke = new SolidColorBrush(Windows.UI.Colors.Red);
            rootPolyline.Points = rootPoints;

            Polyline middlePolyline = new Polyline();
            middlePolyline.StrokeThickness = 1;
            middlePolyline.Stroke = new SolidColorBrush(Windows.UI.Colors.Green);
            middlePolyline.Points = middlePoints;

            Polyline tipPolyline = new Polyline();
            tipPolyline.StrokeThickness = 1;
            tipPolyline.Stroke = new SolidColorBrush(Windows.UI.Colors.Blue);
            tipPolyline.Points = tipPoints;

            graphCanvas.Children.Clear();
            graphCanvas.Children.Add(rootPolyline);
            graphCanvas.Children.Add(middlePolyline);
            graphCanvas.Children.Add(tipPolyline);
        }

        private void activationTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            activationTime = int.Parse(activationTimeTextBox.Text);
        }

        private void timeIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (timer == null)
                return;

            timeInterval = int.Parse(timeIntervalTextBox.Text);
            timer.Change(timeInterval, timeInterval);
        }

        private void distanceDivisionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            distanceDivision = int.Parse(distanceDivisionTextBox.Text);
            distanceInterval = 2.0 / distanceDivision;
        }

        private void frequencyValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
#if CONSOLE_OUTPUT
			Console.WriteLine("f" + frequencySlider.Value);
#endif
            port.WriteLine("f" + frequencySlider.Value);
        }

        private void Window_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.KeyDown += new KeyEventHandler(MainWindow_KeyDown);
        }

        void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                if (currentIndex >= actuatorNum * settingNum * fingerNum)
                {
                    progressTextBlock.Text = "Press R to restart";
                    return;
                }

                int currentSetting;

                if (currentIndex >= 0)
                {
                    currentSetting = order[currentIndex];
                    WriteOneLine(currentSetting / settingNum / fingerNum + ", " + (currentSetting / fingerNum) % settingNum + ", " + currentSetting % fingerNum + ", " + feedbackPointSlider.Value);
                }

                currentIndex++;
                if (currentIndex >= actuatorNum * settingNum * fingerNum)
                {
                    fingerCanvas.Children.RemoveAt(1);
                    progressTextBlock.Text = "Press R to restart";
                    return;
                }

                currentSetting = order[currentIndex];
                setParams(currentSetting / settingNum / fingerNum, (currentSetting / fingerNum) % settingNum);
                // Console.WriteLine("actuator: " + (currentSetting / settingNum / fingerNum + 2) + ", setting: " + ((currentSetting / fingerNum) % settingNum) + ", finger: " + ((currentSetting % fingerNum) + 3));

                progressTextBlock.Text = (currentIndex + 1) + " / " + actuatorNum * settingNum * fingerNum;

                double canvasHeight = fingerCanvas.ActualHeight;
                double canvasWidth = fingerCanvas.ActualWidth;
                Ellipse targetPointer = new Ellipse();
                targetPointer.Height = targetPointer.Width = 20;
                targetPointer.Fill = new SolidColorBrush(Windows.UI.Colors.Red);
                targetPointer.SetValue(Canvas.LeftProperty, canvasWidth / 2 - 10);
                targetPointer.SetValue(Canvas.ZIndexProperty, canvasHeight / 12 * (3 + currentSetting % 7) - 10);
                if (currentIndex > 0)
                    fingerCanvas.Children.RemoveAt(1);
                fingerCanvas.Children.Add(targetPointer);
            }

            else if (e.Key == VirtualKey.R && currentIndex >= actuatorNum * settingNum * fingerNum)
            {
                initExperiment();
                progressTextBlock.Text = "Press enter to start";
            }
        }

        private void initParams()
        {
            frequencySlider.Value = 235;

            rootMaxAmplitudeSlider.Value = 1;
            middleMaxAmplitudeSlider.Value = 0.725;
            tipMaxAmplitudeSlider.Value = 0.3;

            activationTimeTextBox.Text = "15";
            timeIntervalTextBox.Text = "200";
        }

        private void setParams(int actuator, int setting)
        {
            actuatorComboBox.SelectedIndex = actuator;

            switch (setting)
            {
                case 0:
                    functionComboBox.SelectedIndex = 0;
                    constantSlider.Value = 1;
                    break;
                case 1:
                    functionComboBox.SelectedIndex = 1;
                    constantSlider.Value = 9;
                    break;
                case 2:
                    functionComboBox.SelectedIndex = 2;
                    constantSlider.Value = 6.57378;
                    break;
            }

            /*
            switch (setting)
            {
                case 0:
                    functionComboBox.SelectedIndex = 0;
                    constantSlider.Value = 0.5;
                    break;
                case 1:
                    functionComboBox.SelectedIndex = 0;
                    constantSlider.Value = 1;
                    break;
                case 2:
                    functionComboBox.SelectedIndex = 0;
                    constantSlider.Value = 2;
                    break;
                case 3:
                    functionComboBox.SelectedIndex = 1;
                    constantSlider.Value = 9;
                    break;
                case 4:
                    functionComboBox.SelectedIndex = 2;
                    constantSlider.Value = 6.57378;
                    break;
                case 5:
                    functionComboBox.SelectedIndex = 3;
                    constantSlider.Value = 1.16556;
                    break;
                case 6:
                    functionComboBox.SelectedIndex = 3;
                    constantSlider.Value = 1.39325;
                    break;
            }
            */
        }

        public void Shuffle<T>(IList<T> list)
        {
            Random random = new Random();

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        class TimerState
        {
            public int Counter;
        }
    }
}