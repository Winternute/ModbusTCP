using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using ModbusTCP;
using System.Collections;

namespace ModbusApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private ModbusConnection modbus;
        private ModbusMaster master;
        private Timer timer;
        private Timer timer2;

        private void button1_Click(object sender, EventArgs e)        
        {

            if (modbus != null && modbus.IsActive)
            {
                try
                {                    
                    master.WriteSingleCoil(0, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            timer.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            modbus?.Close();
            Properties.Settings.Default.Save();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            IPAddress ip = new IPAddress(new byte[] { 127, 0, 0, 2 }); ;
            if (IPAddress.TryParse(Properties.Settings.Default.IpAddress, out IPAddress ipAddress))          
                ip = ipAddress;

            int port = 502;
            if (int.TryParse(Properties.Settings.Default.Port.Trim(), out int result))            
                port = result;            

            int recieveTimeout = 1000;
            if (int.TryParse(Properties.Settings.Default.RecieveTimeout.Trim(), out int r))
                recieveTimeout = r;

            int sendTimeout = 1000;
            if (int.TryParse(Properties.Settings.Default.SendTimeout.Trim(), out int s))
                sendTimeout = s;

            if (modbus == null)
                modbus = new ModbusConnection(ip, port, recieveTimeout, sendTimeout);

            timer = new Timer();
            timer.Tick += Timer_Tick;

            timer2 = new Timer();
            timer2.Tick += Timer2_Tick;       


            modbus.Connect();
            master = new ModbusMaster(modbus);

            if (modbus != null && modbus.IsActive)
            {
                try
                {
                    textBox1.Text = (master.ReadHoldingRegisters(0, 1)).ToString();
                }
                catch (Exception ex)
                {

                    MessageBox.Show(ex.Message);
                }
            }

        }

        private void Timer2_Tick(object sender, EventArgs e)
        {
            if (modbus != null && modbus.IsActive)
            {
                try
                {
                    var b = master.ReadCoils(0, 1);
                    if (b!=null && b[2])
                    {
                        master.WriteSingleCoil(2, false);
                        Application.Exit();
                    }  
                }
                catch (Exception ex)
                {

                    //MessageBox.Show(ex.Message);
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            if (modbus != null && modbus.IsActive)
            {
                try
                {
                    master.WriteSingleCoil(0, false);
                }
                catch (Exception ex)
                {

                    MessageBox.Show(ex.Message);
                }
            }            
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

            if (modbus != null && modbus.IsActive)
            {
                try
                {
                    master.WriteSingleCoil(1, checkBox1.Checked);                                       
                }
                catch (Exception ex)
                {

                    MessageBox.Show(ex.Message);
                }
            }
        }
    }
}
