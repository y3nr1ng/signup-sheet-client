﻿using signup_sheet_client.Network;
using signup_sheet_client.Panels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace signup_sheet_client
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            // Setup the forms.
            this.displayMessage.AutoSize = false;
            this.displayMessage.Dock = DockStyle.Fill;
            this.displayMessage.Show();

            this.displayUserInfo.AutoSize = false;
            this.displayUserInfo.Dock = DockStyle.Fill;
            this.displayUserInfo.Show();

            this.displayRegion.Controls.Add(this.displayUserInfo);

            // Setup timer.
            this.timer.Interval = displayInterval;

            // Set the status bar.
            this.cardReaderStatus.Text = "Disconnected.";
            this.cardReaderStatus.ForeColor = Color.Black;

            this.applicationStatus.Text = string.Empty;
            this.applicationStatus.ForeColor = Color.Black;
        }

        #region Card reader related functions.

        private ReaderWrapper cardReader = new ReaderWrapper();
        // USB = 100, hard-coded.
        private const short defaultCardReaderPort = 100;
        private bool cardReaderConnected = false;

        private void connectCardReader_Click(object sender, EventArgs e)
        {
            // Connect.
            this.cardReaderConnected = this.cardReader.Open(defaultCardReaderPort);

            // Set the status bar.
            if(this.cardReaderConnected)
            {
                this.cardReaderStatus.Text = "Connected.";
                this.cardReaderStatus.ForeColor = Color.Black;

                // Change menu state if connected.
                this.disconnectCardReader.Visible = this.cardReaderConnected;
                this.connectCardReader.Visible = !this.disconnectCardReader.Visible;
            
                // Start the background worker.

            }
            else
            {
                this.cardReaderStatus.Text = "Fail to connect.";
                this.cardReaderStatus.ForeColor = Color.Red;
            }
        }

        private void disconnectCardReader_Click(object sender, EventArgs e)
        {
            // Stop the background worker.
            this.scanForCard.RunWorkerAsync();

            // Disconnect.
            this.cardReaderConnected = !this.cardReader.Close();

            // Check if successfully closed.
            if(!this.cardReaderConnected)
            {
                // Change menu state if connected.
                this.disconnectCardReader.Visible = this.cardReaderConnected;
                this.connectCardReader.Visible = !this.disconnectCardReader.Visible;

                this.cardReaderStatus.Text = "Disconnected.";
                this.cardReaderStatus.ForeColor = Color.Black;
            }
            else
            {
                this.cardReaderStatus.Text = "Fail to disconnect.";
                this.cardReaderStatus.ForeColor = Color.Red;
            }
        }

        #endregion

        private string serverAddress = string.Empty;

        private void setAddress_Click(object sender, EventArgs e)
        {
            using(AskForServer dialog = new AskForServer(this.serverAddress))
            {
                DialogResult status = dialog.ShowDialog();
                if(status == DialogResult.OK)
                {
                    // Update current server address.
                    this.serverAddress = dialog.Address;
                }
            }
        }

        #region Program stop.

        private void exit_Click(object sender, EventArgs e)
        {
            // Stop the background worker.
            if(this.scanForCard.IsBusy)
            {
                this.scanForCard.CancelAsync();
            }

            // Close the application.
            this.Dispose();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.exit.PerformClick();
        }

        private void scanForCard_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Stop the reader.
            this.disconnectCardReader.PerformClick();
        }

        #endregion

        #region Background worker.

        private Communication network = new Communication();

        private bool blockReading = false;

        private void scanForCard_DoWork(object sender, DoWorkEventArgs e)
        {
            string cardId;
            while(!this.scanForCard.CancellationPending)
            {
                if(!blockReading)
                {
                    cardId = string.Empty;
                    if(this.cardReader.TryRead(out cardId))
                    {
                        // Prevent multiple read.
                        this.blockReading = true;

                        // Print the card ID.
                        this.cardReaderStatus.Text = cardId;
                        this.cardReaderStatus.ForeColor = Color.Blue;

                        this.applicationStatus.Text = "Communicate with " + this.serverAddress + "...";

                        // Network.
                        this.network.Connect(this.serverAddress);
                        this.network.Send(cardId);
                        string data = this.network.Receive();
                        this.scanForCard.ReportProgress(0, new Payload(data));
                        this.network.Disconnect();

                        this.applicationStatus.Text = "End communication.";
                    }
                }
            }
        }

        private DisplayTextMessage displayMessage = new DisplayTextMessage();
        private DisplayUserInfo displayUserInfo = new DisplayUserInfo();

        private const int displayInterval = 2000;

        private void scanForCard_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Payload payload = e.UserState as Payload;
            PayloadParser(payload);
        }
        private void PayloadParser(Payload payload)
        {
            if((!payload.Valid) || (!payload.Due))
            {
                // Set the display message.
                if(!payload.Valid)
                {
                    this.displayMessage.Text = "Invalid user.";
                    this.displayMessage.Color = Color.Red;
                }
                else
                {
                    this.displayMessage.Text = "Due.";
                    this.displayMessage.Color = Color.Black;
                }

                this.displayRegion.Controls.Add(this.displayMessage);
            }
            else
            {
                this.displayUserInfo.RegId = payload.User.RegId;
                this.displayUserInfo.FirstName = payload.User.FirstName;
                this.displayUserInfo.LastName = payload.User.LastName;

                this.displayRegion.Controls.Add(this.displayUserInfo);
            }

            timer.Start();
        }
        private void timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();

            this.displayRegion.Controls.Clear();

            // Unblock reading.
            this.blockReading = false;
        }

        #endregion
    }
}
