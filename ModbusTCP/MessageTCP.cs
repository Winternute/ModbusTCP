using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusTCP
{
    public enum EceptionCode : byte
    {
        IllegalFunction = 1, // Function code received in the query is not recognized or allowed by server
        IllegalDataAddress = 2, // Data address of some or all the required entities are not allowed or do not exist in server
        IllegalDataValue = 3,   //Value is not accepted by server
        ServerDeviceFailure = 4, // Unrecoverable error occurred while server was attempting to perform requested action
        Acknowledge = 5,    //Server has accepted request and is processing it, but a long duration of time is required. This response is returned to prevent a timeout error from occurring in the client. client can next issue a Poll Program Complete message to determine whether processing is completed
        ServerDeviceBusy = 6,   //Server is engaged in processing a long-duration command. client should retry later  
        NegativeAcknowledge = 7,    //Server cannot perform the programming functions. Client should request diagnostic or error information from server   
        MemoryParityError = 8,  //Server detected a parity error in memory. Client can retry the request, but service may be required on the server device
        GatewayPathUnavailable = 10,    //Specialized for Modbus gateways. Indicates a misconfigured gateway
        GatewayTargetDeviceFailedToRespond = 11, //Specialized for Modbus gateways. Sent when server fails to respond
    }
    public interface IResponse: IMessageTCP
    {
        void DecodePDU(byte[] data);
        void DecodeMBAP(byte[] data);
        bool IsVerified { get; set; }
    }

    public interface IRequest : IMessageTCP
    {
        byte[] Encode();
    }
    abstract class MessageTCP
    {
        public MBAP Header;
        public PDU Pdu;
    }

    public interface IMessageTCP
    {
        MBAP GetHeader();        
    }

    internal class RequestTCP : MessageTCP, IRequest
    {
        public RequestTCP(ushort startAddress, ushort id)
        {
            Pdu = new PDU()
            {
                StartAddress = startAddress,               
                DataLength = 5 //ushort address + ushort count + FunctionCode
            };

            Header = new MBAP(id, 1)
            {
                Length = 6
            };
        }
        public byte[] Encode()
        {
            byte[] b = new byte[Header.HeaderLength + Pdu.DataLength];
            //Header
            b[0] = BitConverter.GetBytes(Header.TransactionId)[1];
            b[1] = BitConverter.GetBytes(Header.TransactionId)[0];
            b[2] = BitConverter.GetBytes(Header.ProtocolId)[1];
            b[3] = BitConverter.GetBytes(Header.ProtocolId)[0];
            b[4] = BitConverter.GetBytes(Pdu.DataLength + 1)[1];
            b[5] = BitConverter.GetBytes(Pdu.DataLength + 1)[0];
            b[6] = Header.UnitId;
            //PDU
            b[7] = Pdu.FunctionCode;
            b[8] = BitConverter.GetBytes(Pdu.StartAddress)[1];
            b[9] = BitConverter.GetBytes(Pdu.StartAddress)[0];
            b[10] = Encode2()[0];
            b[11] = Encode2()[1];
            return b;
        }
        public virtual byte[] Encode2()
        {
            return new byte[] { 0, 0};
        }

        public MBAP GetHeader()
        {
            return Header;
        }
    }

    internal class RequestReadCoils : RequestTCP
    {
        public RequestReadCoils( ushort valuesCount, ushort startAddress, ushort id) : base(startAddress, id)
        {
            Pdu.ValuesCount = valuesCount;
            Pdu.FunctionCode = (byte)FuctionCode.ReadCoils;
        }
        public override byte[] Encode2()
        {            
            return new byte[] { BitConverter.GetBytes(Pdu.ValuesCount)[1], BitConverter.GetBytes(Pdu.ValuesCount)[0] };
        }
    }
    internal class RequestWriteSingleCoil : RequestTCP
    {

        public RequestWriteSingleCoil(ushort address, bool value, ushort id) : base(address, id)
        {
            Pdu.FunctionCode = (byte)FuctionCode.WriteSingleCoil;
            Pdu.Data = new ushort[] { (ushort)(value ? 65280 : 0) };
        }

        public override byte[] Encode2()
        {
            return new byte[] { BitConverter.GetBytes(Pdu.Data[0])[1], BitConverter.GetBytes(Pdu.Data[0])[0] };
        }
    }

    internal class RequestReadHoldingRegisters : RequestTCP
    {
        public RequestReadHoldingRegisters(ushort address, ushort valuesCount, ushort id) : base(address, id)
        {
            Pdu.FunctionCode = (byte)FuctionCode.ReadHoldingRegisters;
            Pdu.ValuesCount = valuesCount;
        }

        public override byte[] Encode2()
        {
            return new byte[] { BitConverter.GetBytes(Pdu.ValuesCount)[1], BitConverter.GetBytes(Pdu.ValuesCount)[0] };
        }
    }
    internal class RequestWriteSingleHoldingRegister : RequestTCP
    {
        public RequestWriteSingleHoldingRegister(ushort address, ushort value, ushort id) : base(address, id)
        {
            Pdu.FunctionCode = (byte)FuctionCode.ReadHoldingRegisters;
            Pdu.Data = new ushort[] { value };
        }

        public override byte[] Encode2()
        {
            return new byte[] { BitConverter.GetBytes(Pdu.Data[0])[1], BitConverter.GetBytes(Pdu.Data[0])[0] };
        }
    }

    internal class ResponseTCP : MessageTCP, IResponse
    {
        public ResponseTCP()
        {
            Header = new MBAP();
        }

        public void DecodePDU(byte[] data)
        {
            if (data.Length < 2)
            {
                IsVerified = false;
            }
            else
            {
                byte valuesCount = data[1];
                if (data.Length < 2 + valuesCount)
                {
                    IsVerified = false;
                }
                else
                {
                    DecodePDU2(data, valuesCount);

                    if (Pdu.FunctionCode > 128)
                    {
                        throw new Exception("Ошибка получения ответа. ErrorCode" + (Pdu.FunctionCode - 128));
                    } 
                    
                    IsVerified = true;
                }
            }
        }
        public virtual void DecodePDU2(byte[] data, byte valuesCount)
        {
            
        }
        public void DecodeMBAP(byte[] data)
        {
            if (data.Length < Header.HeaderLength)
            {
                IsVerified = false;
            }
            else
            {
                Header.TransactionId = (ushort)((data[0] << 8) + data[1]);
                Header.ProtocolId = (ushort)((data[2] << 8) + data[3]);
                Header.Length = (ushort)((data[4] << 8) + data[5]);
                if (Header.Length < 1)
                {
                    IsVerified = false;
                }
                else
                {
                    Header.UnitId = data[6];
                    IsVerified = true;
                }
            }
        }
        public bool IsVerified { get; set; }

        public MBAP GetHeader()
        {
            return Header;
        }
    }

    internal class ResponseReadCoils : ResponseTCP
    {
        public BitArray Coils { get; private set; }

        public override void DecodePDU2(byte[] data, byte valuesCount)
        {
            byte[] b = new byte[data.Length - 2];
            Array.Copy(data, 2, b, 0, data.Length - 2);
            Coils = (new BitArray(b));
        }
    }

    internal class ResponseWriteSingleCoil : ResponseTCP
    {
        public override void DecodePDU2(byte[] data, byte valuesCount)
        {
            byte[] b = new byte[data.Length - 2];
            Array.Copy(data, 2, b, 0, data.Length - 2);
        }
    }

    internal class ResponseReadHoldingRegisters : ResponseTCP
    {
        public override void DecodePDU2(byte[] data, byte valuesCount)
        {
            Pdu.Data = new ushort[valuesCount / 2];
            for (int i = 0; i < (valuesCount / 2); i++)
            {
                Pdu.Data[i] = (ushort)((data[i * 2] << 8) + data[1 + i * 2]);
            }
        }
    }

    internal class ResponseWriteSingleHoldingRegister : ResponseTCP
    {
        public override void DecodePDU2(byte[] data, byte valuesCount)
        {
            Pdu.Data = new ushort[] { (ushort)((data[data.Length - 2] << 8) + data[data.Length - 1]) };            
        }
    }

    //Header for Modbus TCP/IP ADU
    public class MBAP
    {
        //Identification of a Modbus Request Response transaction.
        public ushort TransactionId { get; set; }
        //0 = Modbus
        public ushort ProtocolId { get; set; }
        //The length field is a byte count of the following fields, including the Unit Identifier and data fields
        public ushort Length { get; set; }
        //Identification of a remote slave connected on a serial line or on other buses
        public byte UnitId;

        public int HeaderLength { get { return 7; } }
        public int PDULength { get; set; } //{ get { return Length - 1; } }

        public MBAP()
        {
            TransactionId = 1;
            ProtocolId = 0;
            UnitId = 1;
        }

        public MBAP(ushort transactionID, byte unitID)
        {
            TransactionId = transactionID;
            UnitId = unitID;
            ProtocolId = 0;
        }
    }

    public class PDU
    {        
        public byte FunctionCode { get; set; }

        public ushort StartAddress { get; set; }

        public ushort ValuesCount { get; set; }

        public ushort[] Data { get; set; }

        public byte DataLength { get; set; }
    }
}


