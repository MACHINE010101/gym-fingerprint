using libzkfpcsharp;
using Sample;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

namespace FingerPrint_Reader___RaF_Gym__
{
    public partial class Form1 : Form
    {

        //URL FOR MEN => https://pastebin.com/raw/59KfpNwF
        //URL FOR WOMEN => https://pastebin.com/raw/epWBdyjy
        private static readonly string CurrentVersion = "1.0"; // Current app version
        private static readonly string pastebinUrl = "https://pastebin.com/raw/59KfpNwF"; // Pastebin URL


        TextBox passwordTextBox = new TextBox();

        //PATH FOR WOMENS = \\server\C\projects\RafW\Maindb.mdb
        //PATH FOR MENS = \\server\C\projects\RafW\Maindb.mdb

        string mdbFilePath = @"\\server\C\projects\Raf\Maindb.mdb";


        bool fingerIsRegistered = false;
        bool searchByPhone;
        bool searchByCustomerNumber;

        string CustomerNo, PhoneNumber;

        IntPtr mDevHandle = IntPtr.Zero;
        IntPtr mDBHandle = IntPtr.Zero;
        IntPtr FormHandle = IntPtr.Zero;
        bool bIsTimeToDie = false;
        bool IsRegister = false;
        bool bIdentify = true;
        byte[] FPBuffer;
        int RegisterCount = 0;
        const int REGISTER_FINGER_COUNT = 3;
        string templateString = "";

        byte[][] RegTmps = new byte[3][];
        byte[] RegTmp = new byte[2048];
        byte[] CapTmp = new byte[2048];
        int cbCapTmp = 2048;
        int cbRegTmp = 0;
        int iFid = 1;
        Thread captureThread = null;

        private int mfpWidth = 0;
        private int mfpHeight = 0;

        const int MESSAGE_CAPTURED_OK = 0x0400 + 6;

        [DllImport("user32.dll", EntryPoint = "SendMessageA")]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        public Form1()
        {
            InitializeComponent();
            passwordTextBox.Location = new Point(6, 95); // Position relative to the GroupBox
            passwordTextBox.Size = new Size(214, 100); // Size of the TextBox
            passwordTextBox.PasswordChar = '*'; // Character shown for password masking
            groupBox3.Controls.Add(passwordTextBox);
            dataGridView1.DefaultCellStyle.Font = new Font("Arial", 11); // Adjust font and size
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 11, FontStyle.Bold);

            Task.Run(() => CheckForUpdateAsync(pastebinUrl));

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            FormHandle = this.Handle;
            SearchCust.Enabled = true;
            searchByCustomerNumber = true;
            searchByPhone = false;
            checkBox1.Checked = true;
        }

        private void bnInit_Click(object sender, EventArgs e)
        {
            cmbIdx.Items.Clear();
            int ret = zkfperrdef.ZKFP_ERR_OK;
            if ((ret = zkfp2.Init()) == zkfperrdef.ZKFP_ERR_OK)
            {
                int nCount = zkfp2.GetDeviceCount();
                if (nCount > 0)
                {
                    for (int i = 0; i < nCount; i++)
                    {
                        cmbIdx.Items.Add(i.ToString());
                    }
                    cmbIdx.SelectedIndex = 0;
                    bnInit.Enabled = false;
                    bnFree.Enabled = true;
                    bnOpen.Enabled = true;
                    label13.Text = "تم الاتصال بجهاز البصمة.";
                    label13.ForeColor = Color.Green;
                }
                else
                {
                    zkfp2.Terminate();
                    label13.Text = "تعذر الاتصال بجهاز البصمة.";
                    label13.ForeColor = Color.Red;
                }
            }
            else
            {
                MessageBox.Show("تعذر الاتصال بجهاز البصمة , ret=" + ret + " !");
            }
        }

        private void bnFree_Click(object sender, EventArgs e)
        {
            zkfp2.Terminate();
            cbRegTmp = 0;
            bnInit.Enabled = true;
            bnFree.Enabled = false;
            bnOpen.Enabled = false;
            bnClose.Enabled = false;
            bnEnroll.Enabled = false;
            label13.ForeColor = Color.Red;
            label13.Text = "تم قطع الاتصال بجهاز البصمة.";
        }

        private void bnOpen_Click(object sender, EventArgs e)
        {
            int ret = zkfp.ZKFP_ERR_OK;
            if (IntPtr.Zero == (mDevHandle = zkfp2.OpenDevice(cmbIdx.SelectedIndex)))
            {
                label13.ForeColor = Color.Red;
                label13.Text = "تعذر فتح جهاز البصمة."; 
                return;
            }
            if (IntPtr.Zero == (mDBHandle = zkfp2.DBInit()))
            {
                label13.ForeColor = Color.Red;
                label13.Text = "تعذر فتح جهاز البصمة."; 
                zkfp2.CloseDevice(mDevHandle);
                mDevHandle = IntPtr.Zero;
                return;
            }
            bnInit.Enabled = false;
            bnFree.Enabled = true;
            bnOpen.Enabled = false;
            bnClose.Enabled = true;
            bnEnroll.Enabled = true;
            RegisterCount = 0;
            cbRegTmp = 0;
            iFid = 1;
            for (int i = 0; i < 3; i++)
            {
                RegTmps[i] = new byte[2048];
            }
            byte[] paramValue = new byte[4];
            int size = 4;
            zkfp2.GetParameters(mDevHandle, 1, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

            size = 4;
            zkfp2.GetParameters(mDevHandle, 2, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

            FPBuffer = new byte[mfpWidth * mfpHeight];

            //captureThread = new Thread(new ThreadStart(DoCapture));
            captureThread.IsBackground = true;
            captureThread.Start();
            bIsTimeToDie = false;
            label13.ForeColor = Color.Green;
            label13.Text = "تم فتح جهاز البصمة.";
        }

        //private void DoCapture()
        //{
        //    while (!bIsTimeToDie)
        //    {
        //        cbCapTmp = 2048;
        //        int ret = zkfp2.AcquireFingerprint(mDevHandle, FPBuffer, CapTmp, ref cbCapTmp);
        //        if (ret == zkfp.ZKFP_ERR_OK)
        //        {
        //            SendMessage(FormHandle, MESSAGE_CAPTURED_OK, IntPtr.Zero, IntPtr.Zero);
        //        }
        //        Thread.Sleep(200);
        //    }
        //}

        private byte[] GetFingerprintTemplateFromDB(int customerId)
        {
            string mdbFilePath = @"D:\playground\Testdb.mdb";
            string connectionString = $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

            string query = "SELECT Fingerprint FROM Customers WHERE Cust_No = ?";

            using (OleDbConnection connection = new OleDbConnection(connectionString))
            {
                connection.Open();
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    command.Parameters.AddWithValue("?", customerId);
                    string base64Template = (string)command.ExecuteScalar();

                    if (!string.IsNullOrEmpty(base64Template))
                    {
                        return Convert.FromBase64String(base64Template);
                    }
                }
            }
            return null;
        }
        private void PopulateSubscriptions()
        {
            string mdbFilePath = @"D:\playground\Testdb.mdb";
            string connectionString = $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

            string query = @"
        INSERT INTO Subscribe (Cust_No, Start_Date, End_Date, Pay_Date, Total_Amount, 
                              Payed, NotPayed, Last_PayDate, Pay_Method, SubPeriod) 
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

            using (OleDbConnection connection = new OleDbConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    OleDbTransaction transaction = connection.BeginTransaction();

                    using (OleDbCommand command = new OleDbCommand(query, connection, transaction))
                    {
                        // Add parameters...

                        Random random = new Random();
                        string[] paymentMethods = { "Cash", "Credit Card", "Bank Transfer", "PayPal" };
                        string[] periods = { "Monthly", "Quarterly", "Annual" };

                        for (int custId = 1; custId <= 4000; custId++)
                        {
                            DateTime startDate = DateTime.Today.AddDays(-random.Next(365));
                            DateTime endDate = startDate.AddDays(30 * random.Next(1, 13)); // 1-12 months

                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("?", custId);
                            command.Parameters.AddWithValue("?", startDate);
                            command.Parameters.AddWithValue("?", endDate);
                            command.Parameters.AddWithValue("?", startDate.AddDays(random.Next(5)));
                            command.Parameters.AddWithValue("?", 50 + random.Next(200));
                            command.Parameters.AddWithValue("?", 50 + random.Next(200));
                            command.Parameters.AddWithValue("?", 0); // NotPayed
                            command.Parameters.AddWithValue("?", startDate.AddDays(random.Next(5)));
                            command.Parameters.AddWithValue("?", paymentMethods[random.Next(paymentMethods.Length)]);
                            command.Parameters.AddWithValue("?", periods[random.Next(periods.Length)]);

                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    // Error handling
                }
            }
        }
        private void PopulateDatabase()
        {
            string mdbFilePath = @"D:\playground\Testdb.mdb";
            string connectionString = $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

            // Clear existing data first
            string clearQuery = "DELETE FROM Customers";

            // Insert query with all required fields
            string insertQuery = @"
        INSERT INTO Customers (Cust_No, Cust_Name, SubType, Fingerprint, Cust_Photo) 
        VALUES (?, ?, ?, ?, ?)";

            using (OleDbConnection connection = new OleDbConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // Clear existing data
                    using (OleDbCommand clearCommand = new OleDbCommand(clearQuery, connection))
                    {
                        clearCommand.ExecuteNonQuery();
                    }

                    // Begin transaction for bulk insert
                    OleDbTransaction transaction = connection.BeginTransaction();

                    using (OleDbCommand insertCommand = new OleDbCommand(insertQuery, connection, transaction))
                    {
                        // Add parameters in the correct order
                        insertCommand.Parameters.Add("Cust_No", OleDbType.Integer);
                        insertCommand.Parameters.Add("Cust_Name", OleDbType.VarWChar);
                        insertCommand.Parameters.Add("SubType", OleDbType.VarWChar);
                        insertCommand.Parameters.Add("Fingerprint", OleDbType.LongVarWChar); // For Base64
                        insertCommand.Parameters.Add("Cust_Photo", OleDbType.VarWChar);

                        Random random = new Random();
                        string[] customerTypes = { "Standard", "Premium", "VIP", "Corporate" };

                        for (int i = 1; i <= 4000; i++)
                        {
                            // Generate realistic fingerprint template
                            byte[] fingerprintBytes = GenerateRealisticFingerprintTemplate(400); // 400 bytes is realistic
                            string base64Fingerprint = Convert.ToBase64String(fingerprintBytes);

                            // Set parameter values
                            insertCommand.Parameters["Cust_No"].Value = i;
                            insertCommand.Parameters["Cust_Name"].Value = $"Customer_{i}";
                            insertCommand.Parameters["SubType"].Value = customerTypes[random.Next(customerTypes.Length)];
                            insertCommand.Parameters["Fingerprint"].Value = base64Fingerprint;
                            insertCommand.Parameters["Cust_Photo"].Value = $"/photos/customer_{i}.jpg";

                            insertCommand.ExecuteNonQuery();

                            // Progress reporting
                            if (i % 100 == 0)
                            {
                                Console.WriteLine($"Inserted {i} records...");
                                Debug.WriteLine($"Sample Fingerprint: {base64Fingerprint.Substring(0, 20)}...");
                            }
                        }
                    }

                    transaction.Commit();
                    MessageBox.Show("Database populated successfully with valid Base64 fingerprints");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}\n\n{ex.StackTrace}");
                    Debug.WriteLine($"Full error: {ex.ToString()}");
                }
            }
        }

        private byte[] GenerateRealisticFingerprintTemplate(int size)
        {
            byte[] template = new byte[size];
            new Random().NextBytes(template);

            // Add realistic fingerprint template headers
            if (size > 10)
            {
                // Common fingerprint template header bytes
                template[0] = 0x46; // 'F'
                template[1] = 0x50; // 'P'
                template[2] = 0x54; // 'T'
                template[3] = 0x01; // Version
            }

            return template;
        }

        //TestCapture
        private void DoTestCapture()
        {
            cbCapTmp = 2048;

            CapTmp = GetFingerprintTemplateFromDB(3777);
            // MOCK: Simulate successful acquisition
            int ret = zkfp.ZKFP_ERR_OK;          

            if (ret == zkfp.ZKFP_ERR_OK)
            {
                SendMessage(FormHandle, MESSAGE_CAPTURED_OK, IntPtr.Zero, IntPtr.Zero);
            }
            Thread.Sleep(200); 
        }

        protected override void DefWndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case MESSAGE_CAPTURED_OK:
                    {
                        //MemoryStream ms = new MemoryStream();
                        //BitmapFormat.GetBitmap(FPBuffer, mfpWidth, mfpHeight, ref ms);
                        //Bitmap bmp = new Bitmap(ms);
                        //this.picFPImg.Image = bmp;
                        if (IsRegister)
                        {
                            int ret = zkfp.ZKFP_ERR_OK;
                            int fid = 0, score = 0;
                            ret = zkfp2.DBIdentify(mDBHandle, CapTmp, ref fid, ref score);
                            if (zkfp.ZKFP_ERR_OK == ret)
                            {
                                label13.Text = " البصمة مسجلة من قبل " + fid + " ! ";
                                return;
                            }
                            if (RegisterCount > 0 && zkfp2.DBMatch(mDBHandle, CapTmp, RegTmps[RegisterCount - 1]) <= 0)
                            {
                                label13.Text = "الرجاء الضغط 3 مرات.";
                                return;
                            }
                            Array.Copy(CapTmp, RegTmps[RegisterCount], cbCapTmp);
                            String strBase64 = zkfp2.BlobToBase64(CapTmp, cbCapTmp);
                            byte[] blob = zkfp2.Base64ToBlob(strBase64);
                            RegisterCount++;
                            if (RegisterCount >= REGISTER_FINGER_COUNT)
                            {
                                RegisterCount = 0;
                                if (zkfp.ZKFP_ERR_OK == (ret = zkfp2.DBMerge(mDBHandle, RegTmps[0], RegTmps[1], RegTmps[2], RegTmp, ref cbRegTmp)) &&
                                       zkfp.ZKFP_ERR_OK == (ret = zkfp2.DBAdd(mDBHandle, iFid, RegTmp)))
                                {
                                    iFid++;
                                    try
                                    {
                                        zkfp.Blob2Base64String(RegTmp, 2048, ref templateString);
                                        StoreFingerprintForExistingCustomer(templateString);
                                        IsRegister = false;
                                        cbRegTmp = 0;
                                        RegisterCount = 0;
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("Error : " + ex.Message);
                                        File.AppendAllText("Reference\\Logs.txt", $"\r\nError DefWndProc Exception 1 : {ex.Message}");
                                    }
                                }
                                else
                                {
                                    label13.Text = " مشكلة في الادخال. " + ret;
                                }
                                IsRegister = false;
                                return;
                            }
                            else
                            {
                                label13.Text = " مرات " + (REGISTER_FINGER_COUNT - RegisterCount) + " الرجاء الضغط ";
                            }
                        }
                        else
                        {
                            if (bIdentify)
                            {
                                try
                                {
                                    string mdbFilePath = @"D:\playground\Testdb.mdb";

                                    string connectionString = $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";
                                    string query = "SELECT * FROM Customers";

                                    using (OleDbConnection connection = new OleDbConnection(connectionString))
                                    {
                                        try
                                        {
                                            int x = 0;
                                            connection.Open();
                                            OleDbCommand command = new OleDbCommand(query, connection);
                                            OleDbDataReader reader = command.ExecuteReader();
                                            while (reader.Read())
                                            {
                                                if (!reader.IsDBNull(reader.GetOrdinal("Fingerprint")))
                                                {
                                                    string stringTemplate = reader.GetString(reader.GetOrdinal("Fingerprint"));
                                                    byte[] templateFromDbZk4500 = zkfp.Base64String2Blob(stringTemplate);

                                                    
                                                    if (CapTmp.SequenceEqual(templateFromDbZk4500)) // crude match
                                                    {
                                                        // Found a match
                                                        Console.WriteLine("FOUND A MATCH!!!");
                                                        SearchByCustNo(reader["Cust_No"].ToString(), true);
                                                        textBox9.Text = reader["Cust_No"].ToString();
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show("Error during matching : " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            File.AppendAllText("Reference\\Logs.txt", $"\r\nError DefWndProc Exception 2 : {ex.Message}");
                                        }
                                    }
                                }
                                catch(Exception ex)
                                {
                                    MessageBox.Show("Error : " + ex.Message,"RaF Gym",MessageBoxButtons.OK,MessageBoxIcon.Error);
                                }
                            }
                            else
                            {
                                MessageBox.Show("EMPTY");
                            }
                        }
                    }
                    break;

                default:
                    base.DefWndProc(ref m);
                    break;
            }
        }

        private void CloseDevice()
        {
            if (IntPtr.Zero != mDevHandle)
            {
                bIsTimeToDie = true;
                Thread.Sleep(1000);
                captureThread.Join();
                zkfp2.CloseDevice(mDevHandle);
                mDevHandle = IntPtr.Zero;
            }
        }

        private void bnClose_Click(object sender, EventArgs e)
        {
            CloseDevice();
            RegisterCount = 0;
            Thread.Sleep(1000);
            bnInit.Enabled = false;
            bnFree.Enabled = true;
            bnOpen.Enabled = true;
            bnClose.Enabled = false;
            bnEnroll.Enabled = false;
            label13.ForeColor = Color.Red;
            label13.Text = "تم اغلاق جهاز البصمة.";
        }

        private void bnEnroll_Click(object sender, EventArgs e)
        {
            
            if (!fingerIsRegistered)
            {
                if (!IsRegister)
                {
                    IsRegister = true;
                    RegisterCount = 0;
                    cbRegTmp = 0;
                    label13.Text = "الرجاء الضغط 3 مرات.";
                }
            }
            else
            {
                MessageBox.Show("يوجد بصمة مضافة");
                CustomerNo = null;
            }
        }

        private void bnIdentify_Click(object sender, EventArgs e)
        {
            if(passwordTextBox.Text == "raf20240601")
            {
                DeleteFinger();
            }
            else
            {
                MessageBox.Show("كلمة المرور خطاء الرجاء اعادة المحاولة.","RaF Gym",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }

        private void SearchCust_Click(object sender, EventArgs e)
        {
            if (checkBox1.Checked == false & checkBox2.Checked == false)
            {
                MessageBox.Show("الرجاء اختيار طريقة البحث.", "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (searchByPhone)
            {
                CustomerNo = null;
                if (textBox9.Text.Length != 10)
                {
                    MessageBox.Show("الرجاء ادخال رقم هاتف صحيح او البحث عن طريق رقم الاشتراك." + "\r\n" + textBox9.Text, "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    PhoneNumber = null;
                    CustomerNo = null;
                    PhoneNumber = textBox9.Text;
                    SearchByPhone(PhoneNumber);
                    textBox9.Text = "";
                }
            }
            else
            {
                if (searchByCustomerNumber)
                {
                    PhoneNumber = null;
                    CustomerNo = null;
                    CustomerNo = textBox9.Text;
                    SearchByCustNo(CustomerNo, false);
                    textBox9.Text = "";
                }
            }
        }
        private void textBox9Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (checkBox1.Checked == false & checkBox2.Checked == false)
                {
                    MessageBox.Show("الرجاء اختيار طريقة البحث.", "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (searchByPhone)
                {
                    CustomerNo = null;
                    if (textBox9.Text.Length != 10)
                    {
                        MessageBox.Show("الرجاء ادخال رقم هاتف صحيح او البحث عن طريق رقم الاشتراك." + "\r\n" + textBox9.Text, "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        PhoneNumber = textBox9.Text;
                        SearchByPhone(PhoneNumber);
                    }
                }
                else
                {
                    if (searchByCustomerNumber)
                    {
                        PhoneNumber = null;
                        CustomerNo = textBox9.Text;
                        SearchByCustNo(CustomerNo, false);
                    }
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        private void SearchByCustNo(string CustNo, bool enter)
        {
            string mdbFilePath = @"D:\playground\Testdb.mdb";

            DateTime dateTimeNow = DateTime.Now.Date;
            string connectionString = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";
            string query = @"
SELECT Customers.Cust_Name, Customers.SubType, Customers.Fingerprint, Customers.Cust_Photo, 
       Subscribe.Start_Date, Subscribe.End_Date, Subscribe.Pay_Date, Subscribe.Total_Amount, 
       Subscribe.Payed, Subscribe.NotPayed, Subscribe.Last_PayDate, Subscribe.Pay_Method, 
       Subscribe.SubPeriod, Subscribe.StopFrom1, Subscribe.StopTo1, Subscribe.End_Datex1
FROM Customers
LEFT JOIN Subscribe ON Customers.Cust_No = Subscribe.Cust_No
WHERE Customers.Cust_No = ?
ORDER BY Subscribe.End_Date DESC";

            using (OleDbConnection connection = new OleDbConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (OleDbCommand command = new OleDbCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("?", CustNo);

                        using (OleDbDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                // Create DataTable for DataGridView
                                DataTable subscriptionData = new DataTable();
                                subscriptionData.Columns.Add("Start Date", typeof(string));
                                subscriptionData.Columns.Add("End Date", typeof(string));
                                subscriptionData.Columns.Add("Remaining Days", typeof(string));

                                // Process the first record to populate other UI elements
                                if (reader.Read())
                                {
                                    textBox1.Text = reader["Cust_Name"]?.ToString() ?? "لاتوجد بيانات";
                                    textBox6.Text = reader["SubPeriod"]?.ToString() ?? "لاتوجد بيانات";

                                    // Handle fingerprint
                                    string fingerStatus = reader["Fingerprint"]?.ToString();
                                    if (string.IsNullOrEmpty(fingerStatus))
                                    {
                                        textBox5.Text = "لايوجد بصمة مرتبطة";
                                        textBox5.BackColor = Color.Red;
                                        fingerIsRegistered = false;
                                    }
                                    else
                                    {
                                        textBox5.Text = "يوجد بصمة مرتبطة";
                                        textBox5.BackColor = Color.Green;
                                        fingerIsRegistered = true;
                                    }

                                    // Load photo if available
                                    string imagePath = reader["Cust_Photo"]?.ToString();
                                    pictureBox1.Image = File.Exists(imagePath)
                                        ? Image.FromFile(imagePath)
                                        : Image.FromFile("Reference\\image-not-found.png");

                                    // Handle stop period
                                    DateTime? stopFrom = null;
                                    if (reader["StopFrom1"] != DBNull.Value && DateTime.TryParse(reader["StopFrom1"].ToString(), out var parsedStopFrom))
                                    {
                                        stopFrom = parsedStopFrom;
                                    }

                                    if (enter == true) 
                                    {
                                        //INSERTLOGS(CustNo, reader["Cust_Name"]?.ToString());
                                    }

                                    DateTime? stopTo = null;
                                    if (reader["StopTo1"] != DBNull.Value && DateTime.TryParse(reader["StopTo1"].ToString(), out var parsedStopTo))
                                    {
                                        stopTo = parsedStopTo;
                                    }

                                    if (stopFrom.HasValue && stopTo.HasValue && dateTimeNow >= stopFrom && dateTimeNow <= stopTo)
                                    {
                                        MessageBox.Show($"اشتراك العميل متوقف مؤقتًا خلال هذه الفترة. \r\n ({stopFrom:yyyy/MM/dd} TO {stopTo:yyyy/MM/dd})",
                                            "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        label3.Text = "الاشتراك متوقف مؤقتًا.";
                                        label3.ForeColor = Color.Orange;
                                        textBox3.Text = "متوقف مؤقتًا";
                                        textBox3.ForeColor = Color.Orange;
                                        textBox3.BackColor = Color.White;
                                        return;
                                    }
                                }

                                reader.Close();
                                using (OleDbCommand subscriptionCommand = new OleDbCommand(query, connection))
                                {
                                    subscriptionCommand.Parameters.AddWithValue("?", CustNo);
                                    using (OleDbDataReader subscriptionReader = subscriptionCommand.ExecuteReader())
                                    {
                                        DateTime? activeStartDate = null;
                                        DateTime? activeEndDate = null;
                                        string activeRemainingDays = "غير متوفر";

                                        bool isActiveSubscription = false; // Tracks if there's any active subscription

                                        while (subscriptionReader.Read())
                                        {
                                            // Parse Start Date
                                            DateTime? startDate = null;
                                            if (subscriptionReader["Start_Date"] != DBNull.Value && DateTime.TryParse(subscriptionReader["Start_Date"].ToString(), out var parsedStartDate))
                                            {
                                                startDate = parsedStartDate;
                                            }

                                            // Parse End Date
                                            DateTime? endDate = null;
                                            if (subscriptionReader["End_Date"] != DBNull.Value && DateTime.TryParse(subscriptionReader["End_Date"].ToString(), out var parsedEndDate))
                                            {
                                                endDate = parsedEndDate;
                                            }

                                            // Initialize remaining days and status
                                            string remainingDays = "غير متوفر";

                                            if (startDate.HasValue && endDate.HasValue)
                                            {
                                                if (dateTimeNow >= startDate && dateTimeNow <= endDate) // Active subscription
                                                {
                                                    int daysLeft = (endDate.Value - dateTimeNow).Days;

                                                    // Set active subscription details in textboxes
                                                    if (!isActiveSubscription) // Only update once for the first active subscription found
                                                    {
                                                        isActiveSubscription = true;
                                                        textBox2.Text = startDate.Value.ToString("yyyy/MM/dd"); // Start date
                                                        textBox4.Text = endDate.Value.ToString("yyyy/MM/dd");   // End date
                                                        textBox3.Text = $"متبقي {daysLeft} يوم";              // Detailed status
                                                        textBox3.BackColor = Color.Green;
                                                    }

                                                    remainingDays = $"متبقي {daysLeft} يوم";
                                                }
                                                else if (dateTimeNow > endDate) // Expired subscription
                                                {
                                                    remainingDays = "منتهي";
                                                }
                                                else if (dateTimeNow < startDate) // Not started yet
                                                {
                                                    remainingDays = "لم يبدأ بعد";
                                                }
                                            }

                                            subscriptionData.Rows.Add(
                                                startDate?.ToString("yyyy/MM/dd") ?? "غير متوفر",
                                                endDate?.ToString("yyyy/MM/dd") ?? "غير متوفر",
                                                remainingDays
                                            );
                                        }

                                        // Final Check: If no active subscription exists
                                        if (!isActiveSubscription)
                                        {
                                            textBox2.Text = "";                 // Clear Start date
                                            textBox4.Text = "";                 // Clear End date
                                            textBox3.Text = "منتهي";           // Set expired status
                                            textBox3.BackColor = Color.Red;    // Set red background for expired
                                        }

                                    }
                                }
                                dataGridView1.DataSource = subscriptionData;

                                label3.Text = "تم تحميل الاشتراكات بنجاح.";
                                label3.ForeColor = Color.Green;
                            }
                            else
                            {
                                MessageBox.Show("No customer found with this number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                textBox9.Text = "";
                                dataGridView1.DataSource = null;
                                label3.Text = "لا يوجد اشتراك.";
                                label3.ForeColor = Color.Gray;
                            }
                        }
                    }
                    textBox9.Text = "";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    File.AppendAllText("Reference\\Logs.txt", $"\r\nError SearchByCustNo : {ex.Message}");
                }
            }
        }

        private void SearchByPhone(string Phone)
        {
            if (searchByPhone)
            {
                string connectionString = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";
                string query = @"SELECT Customers.Cust_No FROM Customers WHERE Customers.Cust_mobile = ?";

                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    try
                    {
                        connection.Open();

                        OleDbCommand command = new OleDbCommand(query, connection);
                        command.Parameters.AddWithValue("?", Phone.Trim()); // Ensure trimmed phone input
                        OleDbDataReader reader = command.ExecuteReader();

                        if (reader.Read())
                        {
                            SearchByCustNo(reader["Cust_No"]?.ToString(), false);
                        }
                        else
                        {
                            MessageBox.Show($"لايوجد عميل بهذا الرقم", "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        File.AppendAllText("Reference\\Logs.txt", $"\r\nError SearchByPhone Function : {ex.Message}");
                    }
                }
            }
        }
        private void StoreFingerprintForExistingCustomer(string fingerprintData)
        {
            if(PhoneNumber == null)
            {
                string connectionString = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

                if (string.IsNullOrEmpty(CustomerNo))
                {
                    label13.ForeColor = Color.Red;
                    label13.Text = "الرجاء ادخال رقم المشترك.";
                    return;
                }

                if (!CustomerExists(CustomerNo))
                {
                    label13.ForeColor = Color.Red;
                    label13.Text = "المشترك غير موجود في قاعدة البيانات !!!!";
                    return;
                }

                string query = "UPDATE Customers SET Fingerprint = ? WHERE Cust_No = ?";

                using (OleDbConnection conn = new OleDbConnection(connectionString))
                {
                    try
                    {
                        conn.Open();
                        using (OleDbCommand command = new OleDbCommand(query, conn))
                        {
                            command.Parameters.AddWithValue("?", fingerprintData);
                            command.Parameters.AddWithValue("?", CustomerNo);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                label13.ForeColor = Color.Green;
                                label13.Text = "تم ربط البصمة بنجاح !.";
                            }
                            else
                            {
                                label13.ForeColor = Color.Red;
                                label13.Text = "الرجاء التحقق من رقم المشترك.";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error : " + ex.Message);
                        File.AppendAllText("Reference\\Logs.txt", $"\r\nError StoreFingerprintForExistingCustomer Exception 1 : {ex.Message}");
                    }
                }
            }
            else
            {
                if(CustomerNo == null)
                {
                    string connectionString = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

                    if (string.IsNullOrEmpty(PhoneNumber))
                    {
                        label13.ForeColor = Color.Red;
                        label13.Text = "الرجاء ادخال رقم هاتف المشترك.";
                        return;
                    }

                    if (!CustomerPhoneExists(PhoneNumber))
                    {
                        label13.ForeColor = Color.Red;
                        label13.Text = "رقم الهاتف غير مسجل !!!";
                        return;
                    }

                    string query = "UPDATE Customers SET Fingerprint = ? WHERE Cust_mobile = ?";

                    using (OleDbConnection conn = new OleDbConnection(connectionString))
                    {
                        try
                        {
                            conn.Open();
                            using (OleDbCommand command = new OleDbCommand(query, conn))
                            {
                                command.Parameters.AddWithValue("?", fingerprintData);
                                command.Parameters.AddWithValue("?", PhoneNumber);

                                int rowsAffected = command.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    label13.ForeColor = Color.Green;
                                    label13.Text = "تم ربط البصمة بنجاح !.";
                                }
                                else
                                {
                                    label13.ForeColor = Color.Red;
                                    label13.Text = "الرجاء التحقق من رقم هاتف المشترك.";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error : " + ex.Message);
                            File.AppendAllText("Reference\\Logs.txt", $"\r\nError StoreFingerprintForExistingCustomer Exception 2 : {ex.Message}");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("الرجاء البحث عن المشترك المطلوب ادخال بصمته.");
                }
            }
        }


        private bool CustomerExists(string custNo)
        {
            string connectionString = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

            string query = "SELECT COUNT(*) FROM Customers WHERE Cust_No = ?";

            using (OleDbConnection conn = new OleDbConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    using (OleDbCommand cmd = new OleDbCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("?", custNo);

                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                    File.AppendAllText("Reference\\Logs.txt", $"\r\nError CustomerExists : {ex.Message}");
                    return false;
                }
            }
        }

        private bool CustomerPhoneExists(string phone)
        {
            string connectionString = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

            string query = "SELECT COUNT(*) FROM Customers WHERE Cust_mobile = ?";

            using (OleDbConnection conn = new OleDbConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    using (OleDbCommand cmd = new OleDbCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("?", phone);

                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                    File.AppendAllText("Reference\\Logs.txt", $"\r\nError CustomerPhoneExists : {ex.Message}");
                    return false;
                }
            }
        }
        private void INSERTLOGS(string CustNumber, string CustName)
        {
            string connectionString = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";
            string entryDate = DateTime.Now.ToString("yyyy/MM/dd");
            string entryTime = DateTime.Now.ToString("hh:mm:ss tt");
            string query = @"
                             INSERT INTO TodayT (Cust_No, Cust_Name, Enter_Time, Enter_Date, GoIn ) 
                             VALUES (?, ?, ?, ?, ?)";

            using (OleDbConnection connection = new OleDbConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    OleDbCommand command = new OleDbCommand(query, connection);

                    command.Parameters.AddWithValue("?", CustNumber);
                    command.Parameters.AddWithValue("?", CustName);
                    command.Parameters.AddWithValue("?", entryTime);
                    command.Parameters.AddWithValue("?", entryDate);
                    command.Parameters.AddWithValue("?", "دخل");

                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error inserting log: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    File.AppendAllText("Reference\\Logs.txt", $"\r\nError Insert Logs Function : {ex.Message}");
                }
            }
        }

        private void DeleteFinger()
        {
            if(CustomerNo != null)
            {
                try
                {
                    string connectionString = $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

                    string updateQuery = "UPDATE Customers SET Fingerprint = NULL WHERE Cust_No = @CustNo";

                    using (OleDbConnection connection = new OleDbConnection(connectionString))
                    {
                        try
                        {
                            connection.Open();
                            OleDbCommand command = new OleDbCommand(updateQuery, connection);

                            command.Parameters.AddWithValue("@CustNo", CustomerNo);

                            int rowsAffected = command.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                MessageBox.Show($"تم حذف بصمة العميل رقم <{CustomerNo}>", "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show($"لايوجد عميل بهذا الرقم", "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error during clearing name: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            File.AppendAllText("Reference\\Logs.txt", $"\r\nError during clearing name : {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("General error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    File.AppendAllText("Reference\\Logs.txt", $"\r\nError during connection: {ex.Message}");
                }
            }
            else
            {
                if (PhoneNumber != null)
                {
                    try
                    {
                        string connectionString = @"Provider=Microsoft.Jet.OLEDB.4.0;Data Source=Maindb.mdb;";

                        string updateQuery = "UPDATE Customers SET Fingerprint = NULL WHERE Cust_mobile = @CustNo";

                        using (OleDbConnection connection = new OleDbConnection(connectionString))
                        {
                            try
                            {
                                connection.Open();
                                OleDbCommand command = new OleDbCommand(updateQuery, connection);

                                command.Parameters.AddWithValue("@CustNo", PhoneNumber);

                                int rowsAffected = command.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    MessageBox.Show($"تم حذف بصمة العميل رقم <{PhoneNumber}>", "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else
                                {
                                    MessageBox.Show($"لايوجد عميل بهذا الرقم", "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Error during clearing name: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                File.AppendAllText("Reference\\Logs.txt", $"\r\nError during clearing name : {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("General error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        File.AppendAllText("Reference\\Logs.txt", $"\r\nError during connection: {ex.Message}");
                    }
                }
                else
                {
                    MessageBox.Show($"الرجاء البحث عن عميل.", "RaF Gym", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task CheckForUpdateAsync(string pastebinUrl)
        {
            try
            {
                // Step 1: Fetch the update info from Pastebin
                var updateInfo = await GetUpdateInfoFromPastebin(pastebinUrl);

                if (updateInfo == null)
                {
                    MessageBox.Show("No update info found or the URL is invalid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Log the fetched update info for debugging
                MessageBox.Show($"Fetched update info: Version={updateInfo.Version}, URL={updateInfo.DownloadUrl}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Step 2: Compare the current version with the version from Pastebin
                Version latestVersion = new Version(updateInfo.Version);
                Version currentVersion = new Version(CurrentVersion);

                if (latestVersion > currentVersion)
                {
                    // Step 3: If a new version is available, start downloading
                    MessageBox.Show($"A new version ({updateInfo.Version}) is available. Updating...", "Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await DownloadAndUpdateAsync(updateInfo.DownloadUrl);
                }
                else
                {
                    MessageBox.Show("You already have the latest version.", "Up-to-Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<UpdateInfo> GetUpdateInfoFromPastebin(string pastebinUrl)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    // Download the content from the Pastebin link
                    string pastebinContent = await client.DownloadStringTaskAsync(pastebinUrl);

                    // Example Pastebin content format: "Version=2.0;Url=https://example.com/update.exe"
                    var match = Regex.Match(pastebinContent, @"Version=(\d+\.\d+);Url=(https?://[^\s]+)");
                    if (match.Success)
                    {
                        string version = match.Groups[1].Value;
                        string downloadUrl = match.Groups[2].Value;
                        return new UpdateInfo { Version = version, DownloadUrl = downloadUrl };
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching update info from Pastebin: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private async Task DownloadAndUpdateAsync(string downloadUrl)
        {
            try
            {
                // Step 4: Modify the Dropbox URL to ensure it forces a download
                downloadUrl = ModifyDropboxUrl(downloadUrl);

                // Validate the URL format
                Uri uriResult;
                bool isValidUrl = Uri.TryCreate(downloadUrl, UriKind.Absolute, out uriResult) && uriResult.Scheme == Uri.UriSchemeHttps;

                if (!isValidUrl)
                {
                    MessageBox.Show("The provided update URL is invalid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Step 5: Path to temporarily store the downloaded file
                string tempFilePath = Path.Combine(Path.GetTempPath(), "update.exe");

                using (WebClient client = new WebClient())
                {
                    // Download the file from the URL
                    await client.DownloadFileTaskAsync(new Uri(downloadUrl), tempFilePath);
                }

                // Log after download
                MessageBox.Show($"File downloaded successfully to: {tempFilePath}", "Update Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Step 6: Replace the current application executable
                string currentAppPath = Application.ExecutablePath;
                string backupAppPath = currentAppPath + ".bak";

                // Backup the current application if necessary
                if (File.Exists(backupAppPath))
                {
                    File.Delete(backupAppPath);
                }
                File.Move(currentAppPath, backupAppPath);

                // Replace the old application with the downloaded update
                File.Move(tempFilePath, currentAppPath);

                // Restart the application
                Process.Start(currentAppPath);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating the application: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ModifyDropboxUrl(string url)
        {
            // Check if the URL is from Dropbox and adjust it to force download
            if (url.Contains("dropboxusercontent.com"))
            {
                // Modify the Dropbox URL to force the download (change dl=0 to dl=1)
                return url.Replace("?dl=0", "?dl=1");
            }

            // Return the original URL if it's not a Dropbox link
            return url;
        }

        // Class to hold update info
        private class UpdateInfo
        {
            public string Version { get; set; }
            public string DownloadUrl { get; set; }
        }
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            { 
               checkBox2.Checked = false;
               checkBox1.Checked = true;
               searchByPhone = false;
               searchByCustomerNumber = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DoTestCapture();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                checkBox1.Checked = false;
                checkBox2.Checked = true;
                searchByPhone = true;
                searchByCustomerNumber = false;
            }
        }
    }
}