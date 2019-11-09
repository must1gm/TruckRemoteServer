﻿using System;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing;

namespace TruckRemoteControlServer
{
    public class UDPServer
    {
        public int port = 18250;

        int clientsCount = 0;
        public bool enabled = true;
        public static bool paused = false;

        private UdpClient udpClient;
        private PCController controller = new PCController();

        private Label labelStatus;
        private Button buttonStop;
        private Button buttonStart;

        public UDPServer(Label labelStatus, Button buttonStop, Button buttonStart)
        {
            this.labelStatus = labelStatus;
            this.buttonStart = buttonStart;
            this.buttonStop = buttonStop;
        }

        public void Start()
        {
            Thread thread = new Thread(LaunchServer);
            thread.Start();
        }

        public void LaunchServer()
        {
            enabled = true;
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = IPAddress.Parse("0.0.0.0");
                IPEndPoint localIpEndPoint = new IPEndPoint(ipAddress, port);

                udpClient = new UdpClient(localIpEndPoint);

                StartListeningForConnection();
            }
            catch (Exception)
            {
                PostStatusTextAndColor("Disabled", Color.OrangeRed);
                NotifyButtonsIsConnected(false);
            }
        }

        private void StartListeningForConnection()
        {
            PostStatusTextAndColor("Enabled", Color.ForestGreen);
            NotifyButtonsIsConnected(true);

            IPEndPoint anyIpEndPoint = null;

            while (true)
            {
                byte[] receivedBytes = udpClient.Receive(ref anyIpEndPoint);
                string clientMessage = Encoding.UTF8.GetString(receivedBytes);

                if (clientMessage.Equals("TruckRemoteHello"))
                {
                    Debug.WriteLine("Hello received!");
                    byte[] bytesToAnswer = Encoding.UTF8.GetBytes("Hi!");
                    udpClient.Send(bytesToAnswer, bytesToAnswer.Length, anyIpEndPoint);

                    ListenRemoteClient(anyIpEndPoint);
                    udpClient.Close();

                    if (enabled)
                    {
                        LaunchServer();
                        PostStatusTextAndColor("Enabled", Color.ForestGreen);
                        NotifyButtonsIsConnected(true);
                    }
                }
            }
        }

        private void ListenRemoteClient(IPEndPoint specificClientEndPoint)
        {
            PostStatusTextAndColor("Client connected", Color.ForestGreen);
            IPEndPoint newEndPoint = null;
            udpClient.Connect(specificClientEndPoint);
            udpClient.Client.ReceiveTimeout = 5000;

            try
            {
                while (true)
                {
                    byte[] receivedBytes = udpClient.Receive(ref newEndPoint);

                    if (!newEndPoint.Address.Equals(specificClientEndPoint.Address)) continue;

                    string clientMessage = Encoding.UTF8.GetString(receivedBytes);

                    Debug.WriteLine(clientMessage);

                    if (clientMessage.Contains("paused"))
                    {
                        paused = true;
                        PostStatusTextAndColor("Paused by client", Color.ForestGreen);
                        continue;
                    }
                    else if (paused)
                    {
                        paused = false;
                        PostStatusTextAndColor("Client connected", Color.ForestGreen);
                    }

                    string[] msgParts = clientMessage.Split(',');

                    double accelerometerValue = double.Parse(msgParts[0], CultureInfo.InvariantCulture);
                    bool breakClicked = bool.Parse(msgParts[1]);
                    bool gasClicked = bool.Parse(msgParts[2]);
                    bool leftSignalEnabled = bool.Parse(msgParts[3]);
                    bool rightSignalEnabled = bool.Parse(msgParts[4]);
                    bool parkingBrakeEnabled = bool.Parse(msgParts[5]);

                    controller.updateAccelerometerValue(accelerometerValue);
                    controller.updateBreakGasState(breakClicked, gasClicked);
                    controller.updateTurnSignals(leftSignalEnabled, rightSignalEnabled);
                    controller.updateParkingBrake(parkingBrakeEnabled);
                }
            }
            catch (SocketException)
            {
                return;
            }
        }

        public bool IsConnected()
        {
            return clientsCount > 0;
        }

        public void Stop()
        {
            enabled = false;
            try
            {
                udpClient.Close();
            }
            catch (Exception)
            {
            }
        }

        private void PostStatusTextAndColor(string labelText, Color color)
        {
            labelStatus.BeginInvoke((MethodInvoker)delegate ()
            {
                this.labelStatus.Text = labelText;
                this.labelStatus.ForeColor = color;
            });
        }

        private void NotifyButtonsIsConnected(bool isConnected)
        {
            buttonStop.BeginInvoke((MethodInvoker)delegate ()
            {
                this.buttonStop.Enabled = isConnected;
            });
            buttonStart.BeginInvoke((MethodInvoker)delegate ()
            {
                this.buttonStart.Enabled = !isConnected;
            });
        }
    }
}