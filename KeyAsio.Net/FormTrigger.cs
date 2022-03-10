using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;

namespace KeyAsio.Net
{
    public partial class FormTrigger : Form
    {
        private static HashSet<Keys> _pressingKeys = new HashSet<Keys>();

        public FormTrigger()
        {
            InitializeComponent();
            Visible = false;
            Load += Form1_Load;
            Closed += Form1_Closed;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            StartHook();
            Program.Engine.CreateCacheSound(AppSettings.Default.HitsoundPath);
        }

        private void Form1_Closed(object sender, EventArgs e)
        {
            StopHook();
        }

        private void StartHook()
        {
            Program.GlobalHook.KeyDown += GlobalHook_KeyDown;
            Program.GlobalHook.KeyUp += GlobalHook_KeyUp;
        }

        private void StopHook()
        {
            Program.GlobalHook.KeyDown -= GlobalHook_KeyDown;
            Program.GlobalHook.KeyUp -= GlobalHook_KeyUp;
        }

        private static void GlobalHook_KeyDown(object sender, KeyEventArgs e)
        {
            if (AppSettings.Default.Keys.Contains(e.KeyCode))
            {
                if (_pressingKeys.Contains(e.KeyCode))
                    return;
                Program.Engine.PlaySound(AppSettings.Default.HitsoundPath);
                _pressingKeys.Add(e.KeyCode);
                Console.WriteLine("Add " + e.KeyCode);
            }
        }

        private static void GlobalHook_KeyUp(object sender, KeyEventArgs e)
        {
            if (_pressingKeys.Contains(e.KeyCode))
            {
                _pressingKeys.Remove(e.KeyCode);
                Console.WriteLine("Remove " + e.KeyCode);
            }
        }
    }
}
