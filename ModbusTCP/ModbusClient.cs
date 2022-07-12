using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections;


namespace ModbusTCP
{
    public enum ConnectionState : ushort { Disconnected = 0, Connected = 1, ConnectionError = 2, MBReadError = 3, MBWriteError = 4 }

    //modbus code of command
    public enum FuctionCode : byte
    {
        ReadCoils = 1,
        ReadDiscretsInputs = 2,
        ReadHoldingRegisters = 3,
        ReadInputRegisters = 4,
        WriteSingleCoil = 5,
        WriteSingleHoldingRgisters = 6,
        WriteMultipleCoils = 15,
        WriteMultipleHoldingRegisters = 16
    }

    public class ModbusConnection
    {
        private readonly TcpClient client;
        private readonly IPAddress _ip;
        private readonly int _port;
        private NetworkStream stream;
        public ConnectionState state;
        public ushort TransactionId { get; set; } = 0;

        public bool IsActive { get; private set; }
        public int ConnectionId { get; private set; }

        public ModbusConnection(IPAddress ip, int port, int receiveTimeout, int sendTimeout)
        {
            client = new TcpClient
            {
                ReceiveTimeout = receiveTimeout,
                SendTimeout = sendTimeout
            };
            _ip = ip;
            _port = port;
        }

        public bool Connect()
        {
            if (!IsActive)
            {
                int connectionTimeout = 1000;

                var result = client.BeginConnect(_ip, _port, null, null);
                result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(connectionTimeout));

                if (!client.Connected)
                {
                    OnConnectionMessage(String.Format("Connect({0},{1}): Не удалось установить соединение", _ip.ToString(), _port));
                    state = ConnectionState.ConnectionError;
                    return false;
                }

                client.EndConnect(result);

                stream = client.GetStream();
                state = ConnectionState.Connected;
                IsActive = true;
                OnConnectionMessage(String.Format("Connect({0},{1}): Соединение установлено", _ip.ToString(), _port));
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Close()
        {
            if (state == ConnectionState.Disconnected || state == ConnectionState.ConnectionError)
            {
                state = ConnectionState.Disconnected;
                IsActive = false;
                return;
            }
            else
            {
                stream.Close();
                client.Close();
                OnConnectionMessage(String.Format("Close(): Соединение закрыто"));
                state = ConnectionState.Disconnected;
                IsActive = false;
            }
        }

        public event EventHandler<ConnectionEventArgs> ConnectionMessage = delegate { };
        void OnConnectionMessage(string msg)
        {
            var e = new ConnectionEventArgs(ConnectionId, msg);
            ConnectionMessage(this, e);
        }

        public bool CheckConnection(string message)
        {
            if (state == ConnectionState.Disconnected || state == ConnectionState.ConnectionError)
            {
                OnConnectionMessage(message);
                return false;
            }
            return true;
        }
        public void WriteData(byte[] data)
        {
            stream.Write(data, 0, data.Length);
        }

        public int ReadData(int length, out byte[] headerData)
        {
            headerData = new byte[length];
            //return stream.Read(headerData, 0, length);
            stream.BeginRead(headerData, 0, length,null,null);
            return headerData.Length;
        }
    }

    public class MessageEventArgs : EventArgs
    {
        public String Message { get; set; }

        public MessageEventArgs(string msg)
        {
            Message = msg;
        }
    }

    public class ConnectionEventArgs : EventArgs
    {
        public int Connection { get; set; }
        public String Message { get; set; }

        public ConnectionEventArgs(int connId, string msg)
        {
            Connection = connId;
            Message = msg;
        }
    }

    public class ModbusMaster
    {
        readonly ModbusConnection _connection;

        public ModbusMaster(ModbusConnection connection)
        {
            _connection = connection;
        }

        public BitArray ReadCoils(ushort startAddress, ushort valuesCount)
        {
            _connection.TransactionId++;
            //send request
            RequestReadCoils request = new RequestReadCoils(startAddress, valuesCount, _connection.TransactionId);
            _connection.WriteData(request.Encode());

            // reading response header
            ResponseReadCoils response = new ResponseReadCoils();
            if (Check(response))
            {
                return response.Coils;
            }
            return null;
        }

        public bool WriteSingleCoil(ushort startAddress, bool value)
        {
            _connection.TransactionId++;
            RequestWriteSingleCoil request = new RequestWriteSingleCoil(startAddress, value, _connection.TransactionId);
            _connection.WriteData(request.Encode());

            // reading response header
            ResponseWriteSingleCoil response = new ResponseWriteSingleCoil();
            return Check(response);
        }

        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort valuesCount)
        {
            _connection.TransactionId++;
            RequestReadHoldingRegisters request = new RequestReadHoldingRegisters(startAddress, valuesCount, _connection.TransactionId);
            _connection.WriteData(request.Encode());

            // reading response header
            ResponseReadHoldingRegisters response = new ResponseReadHoldingRegisters();
            if (Check(response))
            {
                return response.Pdu.Data;
            }
            else
                return null;
        }

        public bool WriteSingleHoldingRegister(ushort address, ushort value)
        {
            _connection.TransactionId++;
            RequestWriteSingleHoldingRegister request = new RequestWriteSingleHoldingRegister(address, value, _connection.TransactionId);
            _connection.WriteData(request.Encode());

            // reading response header
            ResponseWriteSingleHoldingRegister response = new ResponseWriteSingleHoldingRegister();
            return Check(response);
        }

        private bool Check(IResponse response)
        {
            _connection.ReadData(response.GetHeader().HeaderLength, out byte[] headerData);
            response.DecodeMBAP(headerData);

            if (response.IsVerified)
            {
                //reading rest response data           
                _connection.ReadData(response.GetHeader().PDULength, out byte[] pduData);
                response.DecodePDU(pduData);
            }
            else if (!response.IsVerified)
            {
                _connection.state = ConnectionState.MBReadError;
                return false;
            }
            return true;
        }

    }
}

