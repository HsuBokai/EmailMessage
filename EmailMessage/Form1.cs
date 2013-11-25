using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using OpenPop.Pop3;
using System.Threading;
using System.IO;
using System.Net.Mail;

namespace EmailMessage
{
    public partial class Form1 : Form
    {
        private String _account, _password;
        private String _last_uid = "0";
        private FileInfo _log_file = new FileInfo("Log.txt");
        private FileInfo _last_uid_file = new FileInfo("Last_uid.txt");
        private string _folder_name = "MailDone";

        public Form1()
        {
            InitializeComponent();
            logToFile("program start!!");
             //FileInfo file = new FileInfo(_folder_name + "/" + "657" + ".txt");
             //if (isFindContent(file.OpenText())) MessageBox.Show("true!!");
             //else MessageBox.Show("false!!");
        }

        const int RETRY_MAX_COUNT = 3;
        protected override void OnLoad(EventArgs e)
        {
            int retryCount = 0;
            using (LoginForm fromLogin = new LoginForm())
            {
                while (retryCount < RETRY_MAX_COUNT && fromLogin.ShowDialog() == DialogResult.OK)
                {
                    /*var id = fromLogin;
                    var password = Password;
                    */
                    String account = fromLogin._account;
                    String password = fromLogin._password;
                    if (VeritfyPermission(account, password))
                    {
                        break;
                    }
                    else
                    {
                        ++retryCount;
                    }
                }
                if (retryCount == RETRY_MAX_COUNT) Application.Exit();
                if (fromLogin.DialogResult == DialogResult.Cancel) Application.Exit();
            }
            base.OnLoad(e);
        }
        bool VeritfyPermission(String account, String password)
        {
            using (Pop3Client client = new Pop3Client())
            {
                client.Connect("mail.ntu.edu.tw", 995, true);
                try
                {
                    client.Authenticate(account, password);
                }
                catch (Exception e)
                {
                    return false;
                }
                form1_initial(client, account, password);
                return true;
            }
        }

        void form1_initial(Pop3Client client, String account, String password)
        {
            List<string> uids = client.GetMessageUids();
            try
            {
                StreamReader sr = _last_uid_file.OpenText();
                _last_uid = sr.ReadLine();
                sr.Close();
            }
            catch
            {
                write_last_uid_file(uids[uids.Count - 1]);
            }
            backgroundWorker1.RunWorkerAsync();
            this.Text = "Hello " + account;
            this._account = account;
            this._password = password;

            string activeDir = Directory.GetCurrentDirectory();
            string newPath = System.IO.Path.Combine(activeDir, _folder_name);
            if (!System.IO.Directory.Exists(newPath))
                System.IO.Directory.CreateDirectory(newPath);
        }

        void write_last_uid_file(String s)
        {
            _last_uid = s;
            StreamWriter sw = _last_uid_file.CreateText();
            sw.WriteLine(_last_uid);
            sw.Close();
        }

        public delegate void MyInvoke();
        public void DoWork()
        {
            MyInvoke mi = new MyInvoke(fetch);
            this.BeginInvoke(mi, new Object[] { });
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                DoWork();
                Thread.Sleep(30 * 1000);
            }
        }

        public void fetch()
        {
            // The client disconnects from the server when being disposed
            using (Pop3Client client = new Pop3Client())
            {
                // Connect to the server
                client.Connect("mail.ntu.edu.tw", 995, true);
                // Authenticate ourselves towards the server
                try
                {
                    client.Authenticate(_account, _password);
                }
                catch
                {
                    return;
                }

                // Fetch all the current uids seen
                List<string> uids = client.GetMessageUids();
                // All the new messages not seen by the POP3 client
                int i = uids.Count - 1;
                while (!uids[i].Equals(_last_uid))
                {
                    OpenPop.Mime.Message unseenMessage = client.GetMessage(i + 1);
                    procMail(uids[i], unseenMessage);
                    --i;
                }

                write_last_uid_file(uids[uids.Count - 1]);
                mail_list.Rows.Clear();
                for (int j = 0; j < 10; ++j)
                {
                    int k = uids.Count - j - 1;
                    OpenPop.Mime.Message m = client.GetMessage(k + 1);
                    mail_list.Rows.Add(new Object[] { uids[k], m.Headers.Subject, m.Headers.From });
                }
                /*
                for (int i = 0; i < 0; i++)
                {
                    int ii = uids.Count - i - 1;
                    string currentUidOnServer = uids[ii];
                    if (!currentUidOnServer.Equals(_last_uid))
                    {
                        // We have not seen this message before.
                        // Download it and add this new uid to seen uids

                        // the uids list is in messageNumber order - meaning that the first
                        // uid in the list has messageNumber of 1, and the second has 
                        // messageNumber 2. Therefore we can fetch the message using
                        // i + 1 since messageNumber should be in range [1, messageCount]
                        OpenPop.Mime.Message unseenMessage = client.GetMessage(ii + 1);
                        raws.Add(new Object[] { unseenMessage.Headers.Subject, unseenMessage.Headers.From });
                            
                        //SaveAndLoadFullMessage(unseenMessage);

                        // Add the uid to the seen uids, as it has now been seen
                        //seenUids.Add(currentUidOnServer);
                    }
                }
                */
                client.Disconnect();
            }
        }
        String parseHeader(StreamReader sr)
        {
            String targetHeader = "X-Ntu-Recipient: ";
            int targetHeaderLength = targetHeader.Length;
            while (!sr.EndOfStream)
            {
                String line = sr.ReadLine();
                int lineLength = line.Length;
                if (lineLength > targetHeaderLength && line.Substring(0, targetHeaderLength) == targetHeader)
                {
                    return line.Substring(targetHeaderLength, lineLength);
                }
            }
            sr.Close();
            return "";
        }

        bool isFindContent(StreamReader sr)
        {
            String targetHeader = "=C2=E5=BE=C7";
            int targetHeaderLength = targetHeader.Length;
            while (!sr.EndOfStream)
            {
                String line = sr.ReadLine();
                int lineLength = line.Length;
                if (lineLength > targetHeaderLength)
                {
                    for (int i = 0; i < lineLength - targetHeaderLength + 1; i++)
                    {
                        if(line.Substring(i, targetHeaderLength) == targetHeader)
                            return true;
                    }
                }
            }
            sr.Close();
            return false;
        }

        void logToFile(String s)
        {
            StreamWriter sw = _log_file.AppendText();
            sw.WriteLine(s);
            sw.Close();
        }

        void procMail(string uid, OpenPop.Mime.Message message)
        {
            FileInfo file = new FileInfo(_folder_name + "/" + uid + ".txt");
            if (file.Exists)
            {
                logToFile(uid + "mail txt file exists but not be processed. >< !!!");
                return;
            }
            else
            {
                logToFile(uid + "mail save to txt file.");
                message.Save(file);
            }

            MailMessage msg1 = message.ToMailMessage();
            msg1.From = new MailAddress(_account + "@ntu.edu.tw", _account, System.Text.Encoding.UTF8);
            msg1.To.Clear();

            if (isFindContent(file.OpenText()))
            {
                msg1.To.Add("ripleyhuang@ntu.edu.tw");
                send_msg(msg1);
                logToFile(uid + " mail content include 醫學 => forword mail. !!!");
                return;
            }

            String value = parseHeader(file.OpenText());
            //MessageBox.Show(value);
            logToFile(uid + " Mail parse header to: " + value);
            if (value == "")
            {
                logToFile(uid + " mail header X-Ntu-Recipient: dose not exist. >< !!!");
                return;
            }
            for (int i = 0; i < value.Length; ++i)
            {
                switch (value[i])
                {
                    case 'f':
                        msg1.To.Add("f@ntu.edu.tw");
                        break;
                    case 's':
                        msg1.To.Add("s@ntu.edu.tw");
                        break;
                    case 'u':
                        switch (value[i + 1])
                        {
                            case 'b':
                                msg1.To.Add("ub@ntu.edu.tw");
                                break;
                            case 'r':
                                msg1.To.Add("ur@ntu.edu.tw");
                                break;
                            case 'd':
                                msg1.To.Add("ud@ntu.edu.tw");
                                break;
                        }
                        break;
                }
            }
            send_msg(msg1);
            logToFile(uid + " Mail Send Successfully ^^!!!");
            /*
            MessageBox.Show("Form1 create success!!");
            MailMessage msg = new MailMessage();
            //msg.To.Add(string.Join(",", MailList.ToArray()));
            msg.To.Add("exp2.718281@gmail.com");
            msg.From = new MailAddress( _account + "@ntu.edu.tw", _account, System.Text.Encoding.UTF8);
            msg.Subject = message.Headers.Subject;
            msg.SubjectEncoding = System.Text.Encoding.UTF8;
            msg.Body = "body is test.";
            msg.IsBodyHtml = true;
            msg.BodyEncoding = System.Text.Encoding.UTF8;//郵件內容編碼
            msg.Priority = MailPriority.Normal;//郵件優先級
            */
        }

        void send_msg(MailMessage msg1)
        {
            SmtpClient MySmtp = new SmtpClient();
            MySmtp.Host = "mail.ntu.edu.tw";
            MySmtp.Port = 587;
            MySmtp.Credentials = new System.Net.NetworkCredential(_account, _password);
            MySmtp.EnableSsl = true;
            //MySmtp.Send(msg1);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }



        /*
        public static OpenPop.Mime.Message SaveAndLoadFullMessage(OpenPop.Mime.Message message)
        {
            // FileInfo about the location to save/load message
            FileInfo file = new FileInfo(message.Headers.Subject + ".txt");

            // Save the full message to some file
            message.Save(file);

            // Now load the message again. This could be done at a later point
            OpenPop.Mime.Message loadedMessage = OpenPop.Mime.Message.Load(file);

            // use the message again
            return loadedMessage;
        }
        */
    }
}

