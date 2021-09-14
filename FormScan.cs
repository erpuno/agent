// MICROSOFT

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Forms;

// KODAK

using TWAIN32;

// EMPIRA

using PdfSharp;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;

// XAMARIN

using System.Net.Mqtt;

// INFOTECH

using N2O;

namespace INFOTECH
{
    public partial class FormScan : Form, IMessageFilter
    {
        public TWAINI twain = new TWAINI();

        public int counter = 77;
        public System.Windows.Forms.NotifyIcon notifyIcon;

        public void SystemTrayIconDoubleClick(object sender, MouseEventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        public void ContextMenuExit(object sender, EventArgs e)
        {
            this.SystemTrayIcon.Visible = false;
            Application.Exit();
            Environment.Exit(0);
        }

        public void ShowNotification(string Source, string Message, string URL)
        {
            notifyIcon.Icon = Properties.Resources.Default;
            notifyIcon.Text = "МІА: Сповіщення";
            notifyIcon.BalloonTipTitle = Source;
            notifyIcon.BalloonTipText = Message;
            notifyIcon.BalloonTipClicked += delegate { System.Diagnostics.Process.Start(URL); };
            notifyIcon.ShowBalloonTip(100);
            notifyIcon.Visible = true;
        }

        PdfDocument doc = new PdfDocument();

        public string GetImageFolder() {
            if (m_formsetup!=null) {
                return m_formsetup.GetImageFolder();
            } else {
                // set to setup rollback directory.
                // todo: check if it can be personal here
                return Directory.GetCurrentDirectory();
            }
        }
 
        public void ProcessPDFPage(int start, int page)
        {
            Console.WriteLine("ProcessPDF {0}-{1}", start, page);
            string aszFilename = Path.Combine(GetImageFolder(), "img-" + string.Format("{0:D6}", page)) + ".tif";
            doc.Pages.Add(new PdfPage());
            XGraphics xgr = XGraphics.FromPdfPage(doc.Pages[page-start]);
            XImage img = XImage.FromFile(aszFilename);
            xgr.DrawImage(img, 0, 0);

        }
      
        public void FinalizePDF(int start, int stop)
        {
            Console.WriteLine("CreatePDF {0}-{1}", start, stop);
            doc.Save(Path.Combine(GetImageFolder(), "doc-" + string.Format("{0:D6}", start) + "-" + string.Format("{0:D6}", stop)) + ".pdf");
            doc.Close();
            doc = new PdfDocument();
        }

        public void Version(object sender, EventArgs e)
        {
            updateCounter();
            ShowNotification("Джерело: МІА:Документообіг",
                             "Повідомлення: Новий документ ЗА-23545",
                             "https://crm.erp.uno");


            MessageBox.Show("Версія: 2.5.1.0\n\nРозробник: ДП «ІНФОТЕХ»", "МІА: Сканування");
        }

        public void OpenUrl(string URL) { System.Diagnostics.Process.Start("https://crm.erp.uno"); }

        public void WindowResize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
        }

        public void WindowClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        public FormScan()
        {
            // Build our form...
            InitializeComponent();

            this.Icon = Properties.Resources.Default;
            this.SystemTrayIcon.Icon = Properties.Resources.Default;
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            // Change the Text property to the name of your application
            this.SystemTrayIcon.Text = "МІА: Сканування Документів";
            this.SystemTrayIcon.Visible = true;

            // Modify the right-click menu of your system tray icon here
            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("Про «МІА: Сканування»", Version);
            menu.MenuItems.Add("Налаштування профілів користувача", m_buttonSetup_Click);
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Завершити програму", ContextMenuExit);

            this.SystemTrayIcon.ContextMenu = menu;
            this.notifyIcon.Icon = Properties.Resources.Default;
            this.notifyIcon.ContextMenu = menu;
            this.notifyIcon.Visible = true;

            this.Resize += WindowResize;
            this.Text = "MIA: Сканування";
            this.FormClosing += WindowClosing;

            TWAIN32.Log.Open("INFOTECH", ".", 1);
            TWAIN32.Log.Info("INFOTECH v" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString());

            try
            {
                twain.deviceEventCallback = DeviceEventCallback;
                twain.scanCallback = ScanCallbackTrigger;
                twain.runInUIThreadDelegate = RunInUiThread;
                twain.Init(this.Handle);
                Console.WriteLine("Initialized {0} {1}", twain, this.Handle);
            }
            catch (Exception exception)
            {
                TWAIN32.Log.Error("Exception - " + exception.Message);
                twain.Twain = null;
                twain.Exit = true;
                MessageBox.Show
                (
                    "Unable to start, the most likely reason is that the TWAIN\n" +
                    "Data Source Manager is not installed on your system.\n\n" +
                    "An internet search for 'TWAIN DSM' will locate it and once\n" +
                    "installed, you should be able to proceed.\n\n" +
                    "You can also try the following link:\n" +
                    "https://github.com/erpuno/scan",
                    "Error Starting «MIA: Scanning»"
                );
                return;
            }

            InitImage();
            SetMessageFilter(true);
            SetButtons(EBUTTONSTATE.CLOSED);
        }

        public void updateCounter()
        {
            Brush brush = new SolidBrush(Color.White);
            Font drawFont = new Font("Arial", 8);
            Bitmap bitmap = new Bitmap(16, 16);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.DrawString((counter++).ToString(), drawFont, brush, 0, 1);
            Icon icon = Icon.FromHandle(bitmap.GetHicon());
            this.SystemTrayIcon.Icon = icon;
        }

        public delegate void ScanCallbackEvent();

        public void ScanCallbackEventHandler(object sender, EventArgs e)
        {
            ScanCallbackNative((twain.Twain == null) ? true : (twain.Twain.GetState() <= TWAIN.STATE.S3));
        }

        [SecurityPermissionAttribute(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public bool PreFilterMessage(ref Message a_message)
        {
            if (twain.Twain != null) {
                return (twain.Twain.PreFilterMessage(a_message.HWnd, a_message.Msg, a_message.WParam, a_message.LParam));
            }
            return (true);
        }

        public void SetMessageFilter(bool a_blAdd)
        {
            if (a_blAdd) { Application.AddMessageFilter(this); }
                    else { Application.RemoveMessageFilter(this); }
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust", Unrestricted = false)]
        public TWAIN.STS RestoreSnapshot(string a_szFile)
        {
            TWAIN.STS sts;
            byte[] abSettings;
            UInt32 u32Length;
            IntPtr intptrHandle;
            string szCustomdsdata;
            string szStatus;
            CSV csv = new CSV();
            TWAIN.TW_CAPABILITY twcapability;
            TWAIN.TW_CUSTOMDSDATA twcustomdsdata;

            szStatus = "";
            twcapability = default(TWAIN.TW_CAPABILITY);
            twain.Twain.CsvToCapability(ref twcapability, ref szStatus, "0,0,0");
            twain.Twain.DatCapability(TWAIN.DG.CONTROL, TWAIN.MSG.RESETALL, ref twcapability);

            FileStream filestream = null;
            try
            {
                filestream = new FileStream(a_szFile, FileMode.Open);
                u32Length = (UInt32)filestream.Length;
                abSettings = new byte[u32Length];
                filestream.Read(abSettings, 0, abSettings.Length);
            }
            finally
            {
                if (filestream != null) { filestream.Dispose(); }
            }

            // Put it in an intptr...
            intptrHandle = Marshal.AllocHGlobal((int)u32Length);
            Marshal.Copy(abSettings, 0, intptrHandle, (int)u32Length);

            // Set the snapshot, if possible...
            csv.Add(u32Length.ToString());
            csv.Add(intptrHandle.ToString());
            szCustomdsdata = csv.Get();
            twcustomdsdata = default(TWAIN.TW_CUSTOMDSDATA);
            twain.Twain.CsvToCustomdsdata(ref twcustomdsdata, szCustomdsdata);
            sts = twain.Twain.DatCustomdsdata(TWAIN.DG.CONTROL, TWAIN.MSG.SET, ref twcustomdsdata);

            // Cleanup...
            Marshal.FreeHGlobal(intptrHandle);

            // All done...
            return (sts);
        }

        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust", Unrestricted = false)]
        public TWAIN.STS SaveSnapshot(string a_szFile)
        {
            TWAIN.STS sts;
            TWAIN.TW_CUSTOMDSDATA twcustomdsdata;

            // Test...
            if ((a_szFile == null) || (a_szFile == ""))
            {
                return (TWAIN.STS.SUCCESS);
            }

            // Get a snapshot, if possible...
            twcustomdsdata = default(TWAIN.TW_CUSTOMDSDATA);
            sts = twain.Twain.DatCustomdsdata(TWAIN.DG.CONTROL, TWAIN.MSG.GET, ref twcustomdsdata);
            if (sts != TWAIN.STS.SUCCESS)
            {
                TWAIN32.Log.Error("DAT_CUSTOMDSDATA failed...");
                return (sts);
            }

            // Save the data to a file...
            FileStream filestream = null;
            try
            {
                IntPtr intptrInfo;
                filestream = new FileStream(a_szFile, FileMode.Create);
                byte[] abSettings = new byte[twcustomdsdata.InfoLength];
                intptrInfo = twain.Twain.DsmMemLock(twcustomdsdata.hData);
                Marshal.Copy(intptrInfo, abSettings, 0, (int)twcustomdsdata.InfoLength);
                twain.Twain.DsmMemUnlock(twcustomdsdata.hData);
                filestream.Write(abSettings, 0, abSettings.Length);
            }
            finally
            {
                if (filestream != null) { filestream.Dispose(); }
            }

            // Free the memory...
            twain.Twain.DsmMemFree(ref twcustomdsdata.hData);

            // All done...
            return (TWAIN.STS.SUCCESS);
        }
        public TWAIN.STS DeviceEventCallback()
        {
            TWAIN.STS sts;
            TWAIN.TW_DEVICEEVENT twdeviceevent;

            // Drain the event queue...
            while (true)
            {
                // Try to get an event...
                twdeviceevent = default(TWAIN.TW_DEVICEEVENT);
                sts = twain.Twain.DatDeviceevent(TWAIN.DG.CONTROL, TWAIN.MSG.GET, ref twdeviceevent);
                Console.WriteLine("Device Event: {0}", sts);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    break;
                }
            }

            return (TWAIN.STS.SUCCESS);
        }

        public TWAIN.STS ScanCallbackTrigger(bool a_blClosing)
        {
            BeginInvoke(new MethodInvoker(delegate { ScanCallbackEventHandler(this, new EventArgs()); }));
            return (TWAIN.STS.SUCCESS);
        }

        public TWAIN.STS ScanCallbackNative(bool a_blClosing)
        {
            Console.WriteLine("Scan Callback Entered: {0} Sate {1} Range {2}-{3}", twain.Exit, twain.Twain.GetState(), twain.AutoscanStartPage, twain.ImageCount);

            bool blXferDone = false;
            TWAIN.STS sts;
            TWAIN.TW_USERINTERFACE twuserinterface = default(TWAIN.TW_USERINTERFACE);
            TWAIN.MSG twainmsg;
            TWAIN.TW_IMAGEINFO twimageinfo = default(TWAIN.TW_IMAGEINFO);
            if (twain.Twain == null) { return (TWAIN.STS.FAILURE); }
            if (a_blClosing) { return (TWAIN.STS.SUCCESS); }

            Console.WriteLine("Scan Callback IsMsgCloseDsReq {0}", twain.Twain.IsMsgCloseDsReq());
            if ((twain.Twain.IsMsgCloseDsReq() && !twain.DisableDsSent) || twain.Exit)
            {
                Console.WriteLine("IsMsgCloseDsReq {0}", !twain.DisableDsSent);
                twain.DisableDsSent = true;
                twain.Rollback(TWAIN.STATE.S4);
                SetButtons(EBUTTONSTATE.OPEN);
            }
/*
            if (this.InvokeRequired)
            {
                return
                (
                    (TWAIN.STS)Invoke
                    (
                        (Func<TWAIN.STS>)delegate
                        {
                            return (ScanCallbackNative(a_blClosing));
                        }
                    )
                );
            }
 */
            Console.WriteLine("Scan Callback IsMsgCloseDsOk {0}", twain.Twain.IsMsgCloseDsOk());
            if (twain.Twain.IsMsgCloseDsOk() && !twain.DisableDsSent)
            {
                Console.WriteLine("IsMsgCloseDsOk {0}", !twain.DisableDsSent);
                twain.DisableDsSent = true;
                twain.Rollback(TWAIN.STATE.S4);
                SetButtons(EBUTTONSTATE.OPEN);
            }

            if (twain.ScanStart) {
                TWAIN.TW_CAPABILITY twcapability;
                twain.ScanStart = false;
                twain.PendingXfers = TWAIN.MSG.ENDXFER;
                twcapability = default(TWAIN.TW_CAPABILITY);
                twcapability.Cap = TWAIN.CAP.ICAP_XFERMECH;
                twain.XferMech = TWAIN.TWSX.NATIVE;
            }

            switch (twain.Twain.GetState())
            {
                default:
                    return TWAIN.STS.SUCCESS;

                case TWAIN.STATE.S5:
                    twain.DisableDsSent = true;
                    twain.Twain.DatUserinterface(TWAIN.DG.CONTROL, TWAIN.MSG.DISABLEDS, ref twuserinterface);
                    SetButtons(EBUTTONSTATE.OPEN);
                    return TWAIN.STS.SUCCESS;

                case TWAIN.STATE.S6:
                    switch (twain.PendingXfers)
                    {
                        default:
                        case TWAIN.MSG.ENDXFER:
                            Console.WriteLine("Pending End");
                            break;

                        case TWAIN.MSG.RESET:
                            Console.WriteLine("Pending Reset");
                            twain.PendingXfers = TWAIN.MSG.ENDXFER;
                            twain.Twain.Rollback(twain.AfterScan);
                            return TWAIN.STS.SUCCESS;

                        case TWAIN.MSG.STOPFEEDER:
                            twain.PendingXfers = TWAIN.MSG.ENDXFER;
                            TWAIN.TW_PENDINGXFERS twpendingxfersStopFeeder = default(TWAIN.TW_PENDINGXFERS);
                            sts = twain.Twain.DatPendingxfers(TWAIN.DG.CONTROL, TWAIN.MSG.STOPFEEDER, ref twpendingxfersStopFeeder);
                            Console.WriteLine("Pending Stop Feeder: {0}", sts);
                            if (sts != TWAIN.STS.SUCCESS) { twain.Twain.Rollback(twain.AfterScan); return (TWAIN.STS.SUCCESS); }
                            break;
                   }
                   break;
            }

            if (twain.XferMech == TWAIN.TWSX.NATIVE)
            {
                bitmap = null;
                sts = twain.Twain.DatImagenativexfer(TWAIN.DG.IMAGE, TWAIN.MSG.GET, ref bitmap);
                Console.WriteLine("ImageNativeXfer(): {0}", sts);
                if (bitmap != null) Console.WriteLine("NATIVE GET: {0} {1} {2}", bitmap.Size, twain.ImageCount++, GetImageFolder());
                string aszFilename = Path.Combine(GetImageFolder(), "img-" + string.Format("{0:D6}", twain.ImageCount)) + ".tif";

                if (sts != TWAIN.STS.XFERDONE)
                {
                    Console.WriteLine("Scanning error: {0}", sts);
                    twain.Twain.Rollback(twain.AfterScan);
                    return (TWAIN.STS.SUCCESS);
                }
                else
                {
                    if (File.Exists(aszFilename)) File.Delete(aszFilename);
                    Console.WriteLine("File: {0}", aszFilename);
                    bitmap.Save(aszFilename, ImageFormat.Bmp);

                    if (twain.ImageCount % 2 == 0) LoadImage(ref m_pictureboxImage1, ref m_graphics1, ref m_bitmapGraphic1, bitmap);
                    else LoadImage(ref m_pictureboxImage2, ref m_graphics2, ref m_bitmapGraphic2, bitmap);

                    twainmsg = TWAIN.MSG.ENDXFER;
                    bitmap = null;
                    blXferDone = true;

                    if (twainmsg == TWAIN.MSG.STOPFEEDER) { twain.PendingXfers = TWAIN.MSG.STOPFEEDER; }
                    else if (twainmsg == TWAIN.MSG.RESET) { twain.PendingXfers = TWAIN.MSG.RESET; }
                }
            }

            else
            {
                Console.WriteLine("Scan: unrecognized ICAP_XFERMECH value..." + twain.XferMech);
                twain.Twain.Rollback(twain.AfterScan);
                return (TWAIN.STS.SUCCESS);
            }

            // Get Image Info (Resolution)
            if (blXferDone)
            {
                if (twimageinfo.BitsPerPixel == 0)
                {
                    twimageinfo = default(TWAIN.TW_IMAGEINFO);
                    sts = twain.Twain.DatImageinfo(TWAIN.DG.IMAGE, TWAIN.MSG.GET, ref twimageinfo);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        Console.WriteLine("ImageInfo failed: ");
                        twain.Twain.Rollback(twain.AfterScan);
                        return (TWAIN.STS.SUCCESS);
                    }
                }
                Console.WriteLine("ImageInfo: " + TWAIN.ImageinfoToCsv(twimageinfo));
            }

            // Finalize transport
            TWAIN.TW_PENDINGXFERS twpendingxfersEndXfer = default(TWAIN.TW_PENDINGXFERS);
            sts = twain.Twain.DatPendingxfers(TWAIN.DG.CONTROL, TWAIN.MSG.ENDXFER, ref twpendingxfersEndXfer);
            Console.WriteLine("END XFER: {0} {1}", twpendingxfersEndXfer.Count, twpendingxfersEndXfer.EOJ);
            if (sts != TWAIN.STS.SUCCESS)
            {
                Console.WriteLine("Scanning error: " + sts);
                twain.Twain.Rollback(twain.AfterScan);
                return (TWAIN.STS.SUCCESS);
            }

            ProcessPDFPage(twain.AutoscanStartPage+1, twain.ImageCount);

            // End page XFER 0 0
            if (twpendingxfersEndXfer.Count == 0)
            {
                Console.WriteLine("Scanning done: " + TWAIN.STS.SUCCESS);
                twain.Twain.Rollback(twain.AfterScan);

                twain.DisableDsSent = true;
                twain.Twain.DatUserinterface(TWAIN.DG.CONTROL, TWAIN.MSG.DISABLEDS, ref twuserinterface);
                SetButtons(EBUTTONSTATE.OPEN);
                twain.ScanStart = true;
                FinalizePDF(twain.AutoscanStartPage+1, twain.ImageCount);
                twain.AutoscanStartPage = twain.ImageCount;
            }

            Application.DoEvents();
            BeginInvoke(new MethodInvoker(delegate { ScanCallbackEventHandler(this, new EventArgs()); }));

            return (TWAIN.STS.SUCCESS);

        }

        [SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase")]
        [SecurityPermissionAttribute(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {

                if (twain != null)
                {
                    twain.Dispose();
                    twain = null;
                }
                if (m_bitmapGraphic1 != null)
                {
                    m_bitmapGraphic1.Dispose();
                    m_bitmapGraphic1 = null;
                }
                if (m_bitmapGraphic2 != null)
                {
                    m_bitmapGraphic2.Dispose();
                    m_bitmapGraphic2 = null;
                }
                if (m_brushBackground != null)
                {
                    m_brushBackground.Dispose();
                    m_brushBackground = null;
                }

               components.Dispose();

            }

          base.Dispose(disposing);
        }

        public bool ExitRequested()
        {
            return (twain.Exit);
        }

        public void m_buttonSetup_Click(object sender, EventArgs e)
        {
            m_formsetup = new FormSetup(this, ref twain.Twain, twain.ProductDirectory);
            m_formsetup.StartPosition = FormStartPosition.CenterParent;
            m_formsetup.ShowDialog(this);
        }

        public void EnsureFormSetup() 
        {
            if (m_formsetup == null)
               m_formsetup = new FormSetup(this, ref twain.Twain, twain.ProductDirectory);
        }

        public void m_buttonScan_Click(object sender, EventArgs e)
        {
            twain.UseBitmap = 0;
            string szTwmemref;
            TWAIN.STS sts;
            EnsureFormSetup();
            twain.Exit = false;
            twain.ScanStart = true;

            if (m_formsetup.IsCustomDsDataSupported()) { szTwmemref = "FALSE,FALSE," + this.Handle; }
            else { szTwmemref = "TRUE,FALSE," + this.Handle; }

            ClearEvents();

            TWAIN.TW_USERINTERFACE twuserinterface = default(TWAIN.TW_USERINTERFACE);
            twain.Twain.CsvToUserinterface(ref twuserinterface, szTwmemref);
            sts = twain.Twain.DatUserinterface(TWAIN.DG.CONTROL, TWAIN.MSG.ENABLEDS, ref twuserinterface); // SCAN SEQ

            if (twuserinterface.ShowUI != 0) { twain.AfterScan = TWAIN.STATE.S5; }
            else { twain.AfterScan = TWAIN.STATE.S4; }

            if (sts == TWAIN.STS.SUCCESS) { SetButtons(EBUTTONSTATE.SCANNING); }
        }

        public void ClearEvents()
        {
            twain.XferReadySent = false;
            twain.DisableDsSent = false;
        }

        public void LoadImage(ref PictureBox a_picturebox, ref Graphics a_graphics, ref Bitmap a_bitmapGraphic, Bitmap a_bitmap)
        {
            // We want to maintain the aspect ratio...
            double fRatioWidth = (double)a_bitmapGraphic.Size.Width / (double)a_bitmap.Width;
            double fRatioHeight = (double)a_bitmapGraphic.Size.Height / (double)a_bitmap.Height;
            double fRatio = (fRatioWidth < fRatioHeight) ? fRatioWidth : fRatioHeight;
            int iWidth = (int)(a_bitmap.Width * fRatio);
            int iHeight = (int)(a_bitmap.Height * fRatio);

            // Display the image...
            a_graphics.FillRectangle(m_brushBackground, m_rectangleBackground);
            a_graphics.DrawImage(a_bitmap, new Rectangle(((int)a_bitmapGraphic.Width - iWidth) / 2, ((int)a_bitmapGraphic.Height - iHeight) / 2, iWidth, iHeight));
            a_picturebox.Image = a_bitmapGraphic;
            a_picturebox.Update();
        }

        public void InitImage()
        {
            // Make sure our picture boxes don't do much work...
            m_pictureboxImage1.SizeMode = PictureBoxSizeMode.Normal;
            m_pictureboxImage2.SizeMode = PictureBoxSizeMode.Normal;

            m_bitmapGraphic1 = new Bitmap(m_pictureboxImage1.Width, m_pictureboxImage1.Height, PixelFormat.Format32bppPArgb);
            m_graphics1 = Graphics.FromImage(m_bitmapGraphic1);
            m_graphics1.CompositingMode = CompositingMode.SourceCopy;
            m_graphics1.CompositingQuality = CompositingQuality.HighSpeed;
            m_graphics1.InterpolationMode = InterpolationMode.Low;
            m_graphics1.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            m_graphics1.SmoothingMode = SmoothingMode.HighSpeed;

            m_bitmapGraphic2 = new Bitmap(m_pictureboxImage1.Width, m_pictureboxImage1.Height, PixelFormat.Format32bppPArgb);
            m_graphics2 = Graphics.FromImage(m_bitmapGraphic2);
            m_graphics2.CompositingMode = CompositingMode.SourceCopy;
            m_graphics2.CompositingQuality = CompositingQuality.HighSpeed;
            m_graphics2.InterpolationMode = InterpolationMode.Low;
            m_graphics2.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            m_graphics2.SmoothingMode = SmoothingMode.HighSpeed;

            m_brushBackground = new SolidBrush(Color.White);
            m_rectangleBackground = new Rectangle(0, 0, m_bitmapGraphic1.Width, m_bitmapGraphic1.Height);
        }

        public void RunInUiThread(Action a_action)
        {
            RunInUiThread(this, a_action);
        }

        static public void RunInUiThread(Object a_object, Action a_action)
        {
            Control control = (Control)a_object;
            if (control.InvokeRequired)
            {
                control.Invoke(new FormScan.RunInUiThreadDelegate(RunInUiThread), new object[] { a_object, a_action });
                return;
            }
            a_action();
        }

        public void SetButtons(EBUTTONSTATE a_ebuttonstate)
        {
            switch (a_ebuttonstate)
            {
                default:
                case EBUTTONSTATE.CLOSED:
                    m_buttonOpen.Enabled = true;
                    m_buttonClose.Enabled = false;
                    m_buttonSetup.Enabled = false;
                    m_buttonScan.Enabled = false;
                    m_buttonStop.Enabled = false;
                    break;

                case EBUTTONSTATE.OPEN:
                    m_buttonOpen.Enabled = false;
                    m_buttonClose.Enabled = true;
                    m_buttonSetup.Enabled = true;
                    m_buttonScan.Enabled = true;
                    m_buttonStop.Enabled = false;
                    break;

                case EBUTTONSTATE.SCANNING:
                    m_buttonOpen.Enabled = false;
                    m_buttonClose.Enabled = false;
                    m_buttonSetup.Enabled = false;
                    m_buttonScan.Enabled = false;
                    m_buttonStop.Enabled = true;
                    break;
            }
        }

        public void m_buttonOpen_Click(object sender, EventArgs e)
        {
            string szIdentity;
            string szDefault = "";
            List<string> lszIdentity = new List<string>();
            FormSelect formselect;
            DialogResult dialogresult;
            TWAIN.STS sts;

            sts = twain.OpenManager();
            if (sts != TWAIN.STS.SUCCESS) { MessageBox.Show("Неможливо відкрити джерело даних"); return; }
            szDefault = twain.GetDefault();
            lszIdentity = twain.GetDataSources();
            if (lszIdentity.Count == 0) { MessageBox.Show("На цій ситемі відсутні TWAIN драйвери..."); return; }

            formselect = new FormSelect(lszIdentity, szDefault);
            formselect.StartPosition = FormStartPosition.CenterParent;
            dialogresult = formselect.ShowDialog(this);
            if (dialogresult != System.Windows.Forms.DialogResult.OK) { twain.Exit = true; return; }
            szIdentity = formselect.GetSelectedDriver();
            if (szIdentity == null) { twain.Exit = true; return; }
            twain.Exit = true;
            foreach (string sz in lszIdentity) if (sz.Contains(szIdentity)){ twain.Exit = false; szIdentity = sz; break; }
            if (twain.Exit) { return; }

            string scannerName = twain.OpenScanner(szIdentity);
            // twain.AutoScan();
            // twain.EnableDuplex();
            twain.NativeTransfer();
            twain.AutoFeed();
            twain.ProgressDriverUI(false);

            this.Text = "МІА: Сканування (" + scannerName + ")";
            SetButtons(EBUTTONSTATE.OPEN);
        }

        public void m_buttonClose_Click(object sender, EventArgs e)
        {
            twain.Rollback(TWAIN.STATE.S2);
            SetButtons(EBUTTONSTATE.CLOSED);
            twain.ProductDirectory = "";

            if (m_formsetup!=null) {
                m_formsetup.Dispose();
                m_formsetup = null;
            }
        }

        public void m_buttonStop_Click(object sender, EventArgs e)
        {
            Console.WriteLine("STOP PRESSED");
            twain.Exit = true;
            twain.PendingXfers = TWAIN.MSG.STOPFEEDER;
//            twain.StopFeeder(); // is not supported on Virtual Scanner, we need to check CAPs before calling StopFeeder
        }

        public enum EBUTTONSTATE
        {
            CLOSED,
            OPEN,
            SCANNING
        }

        public Bitmap bitmap;
        public FormSetup m_formsetup;
        public FormCaps m_formcaps;

        public Bitmap m_bitmapGraphic1;
        public Bitmap m_bitmapGraphic2;
        public Graphics m_graphics1;
        public Graphics m_graphics2;
        public Brush m_brushBackground;
        public Rectangle m_rectangleBackground;

        public delegate void RunInUiThreadDelegate(Object a_object, Action a_action);

    }
}
