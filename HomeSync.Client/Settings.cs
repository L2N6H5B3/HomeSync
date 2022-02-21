using System;
using System.Configuration;
using System.Windows.Forms;

namespace HomeSync.Agent {
    public partial class Settings : Form {

        delegate void SetStatusCallback(string text);
        public event EventHandler<AuthenticationKeyUpdateArgs> AuthenticationKeyUpdateEvent;

        public Settings() {
            InitializeComponent();
            // Get Shared Passkey
            sharedPasskeyTextbox.Text = ConfigurationManager.AppSettings.Get("shared-passkey");
        }

        public void SetStatus(string text) {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.InvokeRequired) {
                SetStatusCallback d = new SetStatusCallback(SetStatus);
                this.Invoke(d, new object[] { text });
            } else {
                this.serverStatus.Text = text;
            }
        }

        private void saveButton_Click(object sender, EventArgs e) {
            // Set Authentication Key in Configuration
            ConfigurationManager.AppSettings.Set("shared-passkey", sharedPasskeyTextbox.Text);
            // Notify of Authentication Key Update
            AuthenticationKeyUpdate(sharedPasskeyTextbox.Text);
            // Hide Window
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


        #region Raise Event ###################################################

        private void AuthenticationKeyUpdate(string passkey) {
            // Create new Authentication Args
            AuthenticationKeyUpdateArgs args = new AuthenticationKeyUpdateArgs {
                passkey = passkey
            };
            // Raise Response Event
            AuthenticationKeyUpdateEvent(this, args);
        }

        #endregion ############################################################
    }

    #region Event Arguments ###################################################

    public class AuthenticationKeyUpdateArgs : EventArgs {
        public string passkey;
    }

    #endregion ################################################################
}
