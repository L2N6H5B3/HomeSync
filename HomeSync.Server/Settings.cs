﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HomeSync.Server {
    public partial class Settings : Form {
        public Settings() {
            InitializeComponent();
            
        }

        private void saveButton_Click(object sender, EventArgs e) {
            Hide();
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e) {
            Show();
            WindowState = FormWindowState.Normal;

        }

        private void Settings_FormClosing(object sender, FormClosingEventArgs e) {
            // If the user is closing the application
            if (e.CloseReason == CloseReason.UserClosing) {
                e.Cancel = true;
                Hide();
            }
        }

        private void exitMenuItem_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        private void settingsMenuItem_Click(object sender, EventArgs e) {
            Show();
            WindowState = FormWindowState.Normal;
        }

    }
}