using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System.Threading;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using WvsBeta.Common.Crypto.Cryptography.Engines;

namespace WvsBeta.Common.Sessions
{
    public class Session
    {
        private string sessionName = "";
        public override string ToString()
        {
            return sessionName;
        }

        private void DoAction(Action<long> pAction, string name)
        {
            MasterThread.Instance.AddCallback(pAction, sessionName + " " + name);
        }


        /// <summary>
        /// Socket we use
        /// </summary>
        private Socket _socket;

        #region Data and encryption

        /// <summary>
        /// IV used for header generation and AES decryption
        /// </summary>
        protected byte[] _decryptIV;

        /// <summary>
        /// IV used for header generation and AES encryption
        /// </summary>
        protected byte[] _encryptIV;


        /// <summary>
        /// Buffer used for receiving packets.
        /// </summary>
        private byte[] _buffer = new byte[64];

        /// <summary>
        /// Position for receiving data.
        /// </summary>
        private int _bufferpos;

        /// <summary>
        /// Lenght of packet to receive.
        /// </summary>
        private int _bufferlen;

        private bool _header;
        private bool _encryption = false;
        private readonly bool _receivingFromServer;

        private ushort _mapleVersion;
        private string _maplePatchLocation;
        private byte _mapleLocale;

        public ushort MapleVersion => _mapleVersion;
        public string MaplePatchLocation => _maplePatchLocation;
        public byte MapleLocale => _mapleLocale;

        public bool Disconnected { get; private set; }
        public bool PreventConnectFromSucceeding { get; set; } = false;

        /// <summary>
        /// Accept more than 0xFFFF of bytes over the wire per frame. This should protect against potential DDoS
        /// </summary>
        public bool UseIvanPacket { get; set; } = false;

        public string TypeName { get; private set; }

        public string IP { get; private set; }
        public ushort Port { get; private set; }

        protected byte[] previousDecryptIV = new byte[4];

        #endregion

        #region MapleCFG Encryption
        private static readonly bool shandaEnabled = false;
        private static readonly bool blockCipherEnabled = true;
        private static readonly bool customShandaEnabled = false;
        private IBufferedCipher _encryptCipher;
        private IBufferedCipher _decryptCipher;
        private ShaniquaCrypto _jankyShanda;
        #endregion


        /// <summary>
        /// Creates a new instance of Session.
        /// </summary>
        /// <param name="pSocket">The socket we use</param>
        public Session(Socket pSocket, string tn)
        {
            TypeName = tn;
            Disconnected = false;
            _socket = pSocket;
            _receivingFromServer = false;

            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 5000);
            _socket.NoDelay = true;

            var remoteIpEndPoint = _socket.RemoteEndPoint as IPEndPoint;
            SetIPEndPoint(remoteIpEndPoint);
        }

        public void SetIPEndPoint(IPEndPoint endpoint)
        {
            IP = endpoint.Address.ToString();
            Port = (ushort)endpoint.Port;
            sessionName = "IP: " + IP + ":" + Port;
        }

        /// <summary>
        /// Connects to the server with the given IP and Port
        /// </summary>
        /// <param name="pIP">IP address to connect to.</param>
        /// <param name="pPort">Port to connect to.</param>
        public Session(string pIP, ushort pPort, string tn)
        {
            TypeName = tn;
            IP = pIP;
            Port = pPort;
            sessionName = "IP: " + IP + ":" + Port;

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Disconnected = true;
            _receivingFromServer = true;
            _mapleVersion = 0;
            _socket.BeginConnect(pIP, pPort, EndConnect, null);
        }

        void EndConnect(IAsyncResult pIAR)
        {
            try
            {
                _socket.EndConnect(pIAR);
            }
            catch (Exception ex)
            {
                ////Console.WriteLine(TypeName + " [ERROR] Could not connect to server: {0}", ex.Message);
                return;
            }
            if (PreventConnectFromSucceeding)
            {
                try { _socket.Shutdown(SocketShutdown.Both); } catch { }
                try { _socket.Disconnect(false); } catch { }
                try { _socket.Close(); } catch { }
                return;
            }
            ////Console.WriteLine(TypeName + " Connected with server!");
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 5000);
            _socket.NoDelay = true;
            Disconnected = false;
            StartReading(2, true);
        }

        public bool Disconnect()
        {
            if (Disconnected) return false;
            try { _socket.Shutdown(SocketShutdown.Both); } catch { }
            try { _socket.Disconnect(false); } catch { }
            try { _socket.Close(); } catch { }
            Trace.WriteLine(TypeName + " Manual disconnection!");
            OnDisconnectINTERNAL("Forced disconnect");
            return true;
        }

        /// <summary>
        /// Starts the reading mechanism.
        /// </summary>
        /// <param name="pLength">Amount of bytes to receive</param>
        /// <param name="pHeader">Do we receive a header?</param>
        private void StartReading(int pLength, bool pHeader = false)
        {
            if (Disconnected) return;
            _header = pHeader;

            if (_buffer.Length < pLength)
            {
                const int PageSize = 128;

                int newSize = pLength;
                if ((pLength % PageSize) != 0) newSize += (PageSize - (pLength % PageSize));
                Array.Resize(ref _buffer, newSize);
            }
            _bufferlen = pLength;
            _bufferpos = 0;
            ContinueReading();
        }

        /// <summary>
        /// Calls Socket.BeginReceive to receive more data.
        /// </summary>
        private void ContinueReading()
        {
            if (Disconnected) return;
            try
            {
                _socket.BeginReceive(_buffer, _bufferpos, _bufferlen - _bufferpos, SocketFlags.None, EndReading, null);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(TypeName + " [ERROR] ContinueReading(): " + ex);
                OnDisconnectINTERNAL("BeginReceive exception");
            }
        }

        /// <summary>
        /// Used as IAsyncResult parser for ContinueReading().
        /// </summary>
        /// <param name="pIAR">The result AsyncCallback makes</param>
        private void EndReading(IAsyncResult pIAR)
        {
            var amountReceived = 0;
            try
            {
                amountReceived = _socket.EndReceive(pIAR);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("EXCEPT" + " : " + ex.ToString());
                amountReceived = 0;
            }
            if (amountReceived == 0)
            {
                OnDisconnectINTERNAL("No data received");
                return;
            }

            if (_encryption && _decryptIV == null)
            {
                OnDisconnectINTERNAL("Received data before IV was sent to client (speedy boy).");
                return;
            }

            // Add amount of bytes received to _bufferpos so we know if we got everything.
            _bufferpos += amountReceived;

            try
            {

                // Check if we got all data. There is _no_ way we would have received more bytes than needed. Period.
                if (_bufferpos == _bufferlen)
                {
                    // It seems we have all data we need
                    // Now check if we got a header
                    if (_header)
                    {
                        if (!_encryption && _receivingFromServer)
                        {
                            // Unencrypted packets have a short header with plain length.
                            var length = (ushort)(_buffer[0] | _buffer[1] << 8);
                            StartReading(length);
                        }
                        else
                        {
                            int length = GetHeaderLength(_buffer, _bufferlen, _decryptIV, _mapleVersion, _receivingFromServer);
                            if (length == HEADER_ERROR_MORE_DATA)
                            {
                                _bufferlen += 4;
                                ContinueReading();
                            }
                            else
                            {
                                StartReading(length);
                            }
                        }
                    }
                    else
                    {
                        Packet packet;
                        if (_encryption)
                        {
                            // Small scope hack; this will be on the stack until the
                            // callback in DoAction leaves scope
                            byte[] tmpIV = new byte[4];
                            Array.Copy(_decryptIV, 0, tmpIV, 0, 4);

                            // Make a copy of the data because it will be transformed

                            var tempBuff = new byte[_bufferlen];
                            Array.Copy(_buffer, 0, tempBuff, 0, _bufferlen);
                            var data = Decrypt(tempBuff, _bufferlen, _decryptIV);

                            packet = new Packet(data, _bufferlen);

                            byte opcode = data[0];

                            DoAction((date) =>
                            {
                                if (Disconnected) return;
                                try
                                {
                                    Array.Copy(tmpIV, 0, previousDecryptIV, 0, 4);
                                    OnPacketInbound(packet);
                                }
                                catch (Exception ex)
                                {
                                    ////Console.WriteLine("Handling Packet Error: {0}", ex.ToString());
                                }
                            }, "Packet handling opcode: " + opcode);
                        }
                        else
                        {
                            _encryption = true; // First packet received or sent is unencrypted. All others are.
                            packet = new Packet(_buffer, _bufferlen);

                            _mapleVersion = packet.ReadUShort();
                            _maplePatchLocation = packet.ReadString();
                            _encryptIV = packet.ReadBytes(4);
                            _decryptIV = packet.ReadBytes(4);
                            _mapleLocale = packet.ReadByte();

                            StartSendAndEncryptLoop();
                            initCipher();
                            packet.Reset();

                            DoAction((date) =>
                            {
                                try
                                {
                                    OnHandshakeInbound(packet);
                                }
                                catch (Exception ex)
                                {
                                    ////Console.WriteLine("Handling Packet Error: {0}", ex.ToString());
                                }
                            }, "Handshake handling");
                        }

                        StartReading(4, true);
                    }
                }
                else
                {
                    ContinueReading();
                }

            }
            catch (SocketException socketException)
            {
                Trace.WriteLine(TypeName + " Socket Exception while receiving data: " + socketException);
                OnDisconnectINTERNAL("EndRead socket exception");
            }
            catch (ObjectDisposedException)
            {
                OnDisconnectINTERNAL("EndRead object disposed");
            }
            catch (Exception ex)
            {
                Trace.WriteLine(TypeName + " [ERROR] EndReading(): " + ex);
                OnDisconnectINTERNAL("EndRead exception");
            }
        }

        public virtual void SendPacket(Packet pPacket)
        {
            SendData(pPacket.ToArray());
        }

        /// <summary>
        /// Sends bytes to the other side
        /// </summary>
        /// <param name="pData">Data to encrypt and send</param>
        public void SendData(byte[] pData)
        {
            if (Disconnected) return;

            _dataToEncrypt.Enqueue(pData);
            _dataReadyToSend.Set();
        }

        static Random rnd = new Random();
        public void SendHandshake(ushort pVersion, string pPatchLocation, byte pLocale)
        {
            Trace.WriteLine("Got connection, sending handshake");

            _encryptIV = new byte[4];
            _decryptIV = new byte[4];
            rnd.NextBytes(_encryptIV);
            rnd.NextBytes(_decryptIV);

            _mapleVersion = pVersion;
            _maplePatchLocation = pPatchLocation;
            _mapleLocale = pLocale;

            initCipher();
            StartSendAndEncryptLoop();

            var packet = new Packet();
            packet.WriteUShort(pVersion);
            packet.WriteString(pPatchLocation);
            packet.WriteBytes(_decryptIV);
            packet.WriteBytes(_encryptIV);
            packet.WriteByte(pLocale);
            SendData(packet.ToArray());
            
            StartReading(4, true);
        }

        internal IBufferedCipher GetCipher(bool cipher = true)
        {
            if (cipher || UseIvanPacket) //if using ivan packet, this is an inter-server socket, and just default to nop crypto
                return new CtsBlockCipher(new CbcBlockCipher(new RC6Engine()));
            return new BufferedBlockCipher(new NopEngine());
        }

        private void initCipher()
        {
            if (blockCipherEnabled)
            {
                _encryptCipher = GetCipher(false);
                _encryptCipher.Init(true, new ParametersWithIV(_keyParameter, new byte[16]));

                _decryptCipher = GetCipher();
                _decryptCipher.Init(false, new ParametersWithIV(_keyParameter, new byte[16]));
            }
            if (customShandaEnabled)
                _jankyShanda = new ShaniquaCrypto();
        }

        private byte[] QuadIv(byte[] iv)
        {
            return new byte[16]
            {
                iv[0], iv[1], iv[2], iv[3],
                iv[0], iv[1], iv[2], iv[3],
                iv[0], iv[1], iv[2], iv[3],
                iv[0], iv[1], iv[2], iv[3],
            };
        }

        public virtual void OnPacketInbound(Packet pPacket)
        {
            ////Console.WriteLine(TypeName + " No Handler for 0x{0:X4}", pPacket.ReadUShort());
        }

        public virtual void OnHandshakeInbound(Packet pPacket)
        {
            ////Console.WriteLine(TypeName + " No Handshake Handler.");
        }

        private void OnDisconnectINTERNAL(string reason)
        {
            if (Disconnected) return;
            Disconnected = true;
            StopSendAndEncryptLoop();

            ////Console.WriteLine(TypeName + " Called by:");
            ////Console.WriteLine(Environment.StackTrace);
            DoAction((date) =>
            {
                OnDisconnect();
            }, "OnDisconnectINTERNAL " + reason);
        }

        public virtual void OnDisconnect()
        {
            if (Disconnected) return;
            Disconnected = true;
            ////Console.WriteLine(TypeName + " Called by:");
            ////Console.WriteLine(Environment.StackTrace);
            ////Console.WriteLine(TypeName + " No Disconnect Handler.");
        }

        #region Send Thread

        private Thread _encryptAndSendThread;
        private readonly ConcurrentQueue<byte[]> _dataToEncrypt = new ConcurrentQueue<byte[]>();
        private readonly AutoResetEvent _dataReadyToSend = new AutoResetEvent(false);


        private void StopSendAndEncryptLoop()
        {
            if (_encryptAndSendThread == null) return;
            _encryptAndSendThread = null;
            // Make the thread stop
            _dataReadyToSend.Set();
        }

        private void StartSendAndEncryptLoop()
        {
            _encryptAndSendThread = new Thread(x =>
            {
                Trace.WriteLine("Starting encryption loop");
                try
                {
                    while (!Disconnected)
                    {
                        _dataReadyToSend.WaitOne();
                        while (
                            !Disconnected &&
                            _dataToEncrypt.TryDequeue(out var data)
                        )
                        {
                            DoSendData(data);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // lets not handle this one
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception in Encrypt and Send thread: {ex}");
                }

                Trace.WriteLine("Stopped encryption loop");
            }) {IsBackground = true, Name = sessionName};
            _encryptAndSendThread.Start();
        }

        private void DoSendData(byte[] data)
        {
            if (Disconnected) return;

            var toSend = new ArraySegment<byte>[2];
            var len = data.Length;

            if (_encryption)
            {
                toSend[0] = new ArraySegment<byte>(GenerateHeader(_encryptIV, len, _mapleVersion, _receivingFromServer));
                toSend[1] = new ArraySegment<byte>(Encrypt(data, len, _encryptIV));
            }
            else
            {
                toSend[0] = new ArraySegment<byte>(new byte[2] { (byte)len, (byte)((len >> 8) & 0xFF) });
                toSend[1] = new ArraySegment<byte>(data);
                _encryption = true; // First packet received or sent is unencrypted. All others are.
            }

            try
            {
                _socket.Send(toSend, SocketFlags.None);
            }
            catch (ObjectDisposedException)
            {
                // Socket is gone
                OnDisconnectINTERNAL("ObjectDisposed");
            }
            catch (Exception ex)
            {
                Trace.WriteLine(TypeName + " [ERROR] Failed sending: " + ex);
                OnDisconnectINTERNAL("General Exception");
            }

            if (Disconnected) return;
        }

        #endregion

        #region Encryption Stuff
        /// <summary>
        /// 256 bytes long shift key, used for MapleStory cryptography and header generation.
        /// </summary>
        public static readonly byte[] sShiftKey = {
            0xEC, 0x3F, 0x77, 0xA4, 0x45, 0xD0, 0x71, 0xBF, 0xB7, 0x98, 0x20, 0xFC, 0x4B, 0xE9, 0xB3, 0xE1,
            0x5C, 0x22, 0xF7, 0x0C, 0x44, 0x1B, 0x81, 0xBD, 0x63, 0x8D, 0xD4, 0xC3, 0xF2, 0x10, 0x19, 0xE0,
            0xFB, 0xA1, 0x6E, 0x66, 0xEA, 0xAE, 0xD6, 0xCE, 0x06, 0x18, 0x4E, 0xEB, 0x78, 0x95, 0xDB, 0xBA,
            0xB6, 0x42, 0x7A, 0x2A, 0x83, 0x0B, 0x54, 0x67, 0x6D, 0xE8, 0x65, 0xE7, 0x2F, 0x07, 0xF3, 0xAA,
            0x27, 0x7B, 0x85, 0xB0, 0x26, 0xFD, 0x8B, 0xA9, 0xFA, 0xBE, 0xA8, 0xD7, 0xCB, 0xCC, 0x92, 0xDA,
            0xF9, 0x93, 0x60, 0x2D, 0xDD, 0xD2, 0xA2, 0x9B, 0x39, 0x5F, 0x82, 0x21, 0x4C, 0x69, 0xF8, 0x31,
            0x87, 0xEE, 0x8E, 0xAD, 0x8C, 0x6A, 0xBC, 0xB5, 0x6B, 0x59, 0x13, 0xF1, 0x04, 0x00, 0xF6, 0x5A,
            0x35, 0x79, 0x48, 0x8F, 0x15, 0xCD, 0x97, 0x57, 0x12, 0x3E, 0x37, 0xFF, 0x9D, 0x4F, 0x51, 0xF5,
            0xA3, 0x70, 0xBB, 0x14, 0x75, 0xC2, 0xB8, 0x72, 0xC0, 0xED, 0x7D, 0x68, 0xC9, 0x2E, 0x0D, 0x62,
            0x46, 0x17, 0x11, 0x4D, 0x6C, 0xC4, 0x7E, 0x53, 0xC1, 0x25, 0xC7, 0x9A, 0x1C, 0x88, 0x58, 0x2C,
            0x89, 0xDC, 0x02, 0x64, 0x40, 0x01, 0x5D, 0x38, 0xA5, 0xE2, 0xAF, 0x55, 0xD5, 0xEF, 0x1A, 0x7C,
            0xA7, 0x5B, 0xA6, 0x6F, 0x86, 0x9F, 0x73, 0xE6, 0x0A, 0xDE, 0x2B, 0x99, 0x4A, 0x47, 0x9C, 0xDF,
            0x09, 0x76, 0x9E, 0x30, 0x0E, 0xE4, 0xB2, 0x94, 0xA0, 0x3B, 0x34, 0x1D, 0x28, 0x0F, 0x36, 0xE3,
            0x23, 0xB4, 0x03, 0xD8, 0x90, 0xC8, 0x3C, 0xFE, 0x5E, 0x32, 0x24, 0x50, 0x1F, 0x3A, 0x43, 0x8A,
            0x96, 0x41, 0x74, 0xAC, 0x52, 0x33, 0xF0, 0xD9, 0x29, 0x80, 0xB1, 0x16, 0xD3, 0xAB, 0x91, 0xB9,
            0x84, 0x7F, 0x61, 0x1E, 0xCF, 0xC5, 0xD1, 0x56, 0x3D, 0xCA, 0xF4, 0x05, 0xC6, 0xE5, 0x08, 0x49
        };


        private static readonly KeyParameter _keyParameter = new KeyParameter(new byte[16]
        {
            sShiftKey[0], sShiftKey[1], sShiftKey[2], sShiftKey[3],
            sShiftKey[4], sShiftKey[5], sShiftKey[6], sShiftKey[7],
            sShiftKey[8], sShiftKey[9], sShiftKey[10], sShiftKey[11],
            sShiftKey[12], sShiftKey[13], sShiftKey[14], sShiftKey[15]
        });

        private void MakeBufferList(int length, Action<(int Offset, int Length)> handler)
        {
            int chunkSize = 1456;
            int offset = 0;
            do
            {
                handler((offset, Math.Min(length - offset, chunkSize)));
                offset += chunkSize;
                chunkSize = 1460;
            } while (offset < length);

        }

        /// <summary>
        /// Encrypts the given data, and updates the Encrypt IV
        /// </summary>
        /// <param name="pData">Data to be encrypted (without header!)</param>
        /// <returns>Encrypted data (with header!)</returns>
        private byte[] Encrypt(byte[] pData, int pLength, byte[] iv)
        {
            if (blockCipherEnabled)
            {
                var qiv = QuadIv(iv);
                MakeBufferList(pLength, b =>
                {
                    var blobLen = b.Length;
                    if (blobLen > 16)
                    {
                        _encryptCipher.Init(true, new ParametersWithIV(null, qiv));
                        _encryptCipher.DoFinal(pData, b.Offset, blobLen, pData, b.Offset);
                    }
                    else
                    {
                        for (var i = 0; i < blobLen; i++)
                        {
                            pData[b.Offset + i] ^= qiv[i % 16];
                        }
                    }
                });

            }

            if (shandaEnabled)
                EncryptMSCrypto(pData, pLength);

            if (customShandaEnabled)
                _jankyShanda.Encrypt(pData, pLength, iv);

            //Trace.WriteLine("Encrypted: " + BitConverter.ToString(cfgEncrypted));

            NextIV(iv);
            return pData;
        }



        /// <summary>
        /// Decrypts given data, and updates the Decrypt IV
        /// </summary>
        /// <param name="pData">Data to be decrypted</param>
        /// <returns>Decrypted data</returns>
        private byte[] Decrypt(byte[] pData, int pLength, byte[] iv)
        {
            if (customShandaEnabled)
                _jankyShanda.Decrypt(pData, pLength, iv);

            if (shandaEnabled)
                DecryptMSCrypto(pData, pLength);

            if (blockCipherEnabled)
            {
                var qiv = QuadIv(iv);
                MakeBufferList(pLength, b =>
                {
                    var blobLen = b.Length;

                    if (blobLen > 16)
                    {
                        _decryptCipher.Init(false, new ParametersWithIV(null, qiv));
                        _decryptCipher.DoFinal(pData, b.Offset, blobLen, pData, b.Offset);
                    }
                    else
                    {
                        for (var i = 0; i < blobLen; i++)
                        {
                            pData[b.Offset + i] ^= qiv[i % 16];
                        }
                    }
                });
            }

            NextIV(iv);
            return pData;
        }

        /// <summary>
        /// Rolls the value left. Port from NLS (C++) _rotl8
        /// </summary>
        /// <param name="value">Value to be shifted</param>
        /// <param name="shift">Position to shift to</param>
        /// <returns>Shifted value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RollLeft(byte value, int shift)
        {
            uint overflow = ((uint)value) << (shift % 8);
            return (byte)((overflow & 0xFF) | (overflow >> 8));
        }

        /// <summary>
        /// Rolls the value right. Port from NLS (C++) _rotr8
        /// </summary>
        /// <param name="value">Value to be shifted</param>
        /// <param name="shift">Position to shift to</param>
        /// <returns>Shifted value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RollRight(byte value, int shift)
        {
            uint overflow = (((uint)value) << 8) >> (shift % 8);
            return (byte)((overflow & 0xFF) | (overflow >> 8));
        }

        /// <summary>
        /// Encrypts given data with the MapleStory cryptography
        /// </summary>
        /// <param name="pData">Unencrypted data</param>
        private static void EncryptMSCrypto(byte[] pData, int pLength)
        {
            int length = pLength, j;
            byte a, c;
            for (var i = 0; i < 3; i++)
            {
                a = 0;
                for (j = length; j > 0; j--)
                {
                    c = pData[length - j];
                    c = RollLeft(c, 3);
                    c = (byte)(c + j);
                    c ^= a;
                    a = c;
                    c = RollRight(a, j);
                    c ^= 0xFF;
                    c += 0x48;
                    pData[length - j] = c;
                }
                a = 0;
                for (j = length; j > 0; j--)
                {
                    c = pData[j - 1];
                    c = RollLeft(c, 4);
                    c = (byte)(c + j);
                    c ^= a;
                    a = c;
                    c ^= 0x13;
                    c = RollRight(c, 3);
                    pData[j - 1] = c;
                }
            }
        }

        /// <summary>
        /// Decrypts given data with the MapleStory cryptography
        /// </summary>
        /// <param name="pData"></param>
        private static void DecryptMSCrypto(byte[] pData, int pLength)
        {
            int length = pLength, j;
            byte a, b, c;
            for (var i = 0; i < 3; i++)
            {
                a = 0;
                b = 0;
                for (j = length; j > 0; j--)
                {
                    c = pData[j - 1];
                    c = RollLeft(c, 3);
                    c ^= 0x13;
                    a = c;
                    c ^= b;
                    c = (byte)(c - j);
                    c = RollRight(c, 4);
                    b = a;
                    pData[j - 1] = c;
                }
                a = 0;
                b = 0;
                for (j = length; j > 0; j--)
                {
                    c = pData[length - j];
                    c -= 0x48;
                    c ^= 0xFF;
                    c = RollLeft(c, j);
                    a = c;
                    c ^= b;
                    c = (byte)(c - j);
                    c = RollRight(c, 3);
                    b = a;
                    pData[length - j] = c;
                }
            }
        }

        internal class ShaniquaCrypto
        {
            #region Encryption
            public void Encrypt(byte[] Data, int Length, byte[] IV)
            {
                if (Data == null)
                    throw new ArgumentNullException(nameof(Data), "Parameter cannot be null.");

                var Key = KeyGen(IV);

                for (int i = 0; i <= 6; i++)
                {
                    if ((i % 3) != 0)
                    {
                        if ((i & 1) != 0)
                            OddEncryptTransform(Data, Length, Key);
                        else
                            EvenEncryptTransform(Data, Length, Key);
                    }
                    else
                        By3EncryptTransform(Data, Length, IV, Key);
                }

                XorBlock(Data, Length, IV);
            }

            private void EvenEncryptTransform(byte[] Data, int Length, byte Key)
            {
                byte Remember = 0;
                for (int i = Length - 1; i >= 0; i--)
                {
                    byte Current = RollLeft(Data[i], -Key % 4);
                    Current += Key;

                    Current ^= Remember;
                    Remember = Current;

                    Current ^= 0xEC;
                    Current = RollRight(Current, ~Key % 3);
                    Data[i] = Current;

                    Key++;
                }
            }

            private void OddEncryptTransform(byte[] Data, int Length, byte Key)
            {
                byte Remember = 0;
                for (int i = 0; i < Length; i++)
                {
                    byte Current = RollLeft(Data[i], Key % 3);
                    Current = (byte)(Key ^ (16 * Current | (Current >> 4)));

                    Current ^= Remember;
                    Remember = Current;

                    Current = RollRight(Current, Key % 4);
                    Current = (byte)(~Current & 0xFF);
                    Current += 0x32;
                    Data[i] = Current;

                    Key--;
                }
            }

            private unsafe void By3EncryptTransform(byte[] Data, int Length, byte[] IV, byte Key)
            {
                fixed (byte* pData = Data)
                {
                    var Blocks = Length / 4;
                    var Snowflakes = Length % 4;

                    if (Blocks > 0)
                    {
                        if (Snowflakes > 0)
                        {
                            for (int i = 0; i < Length; i++)
                            {
                                pData[i] = (byte)(Key ^ (16 * pData[i] | (pData[i] >> 4)));
                            }
                        }

                        uint* pBlocks = (uint*)(pData + Snowflakes);
                        var ReversedIV = (uint)(IV[3] | (IV[2] | (IV[1] | (IV[0]))));

                        for (int i = 0; i < Blocks; i++)
                        {
                            pBlocks[i] = ReversedIV ^ (16 * (pBlocks[i] & 0xFF0F0F0F) | (pBlocks[i] >> 4) & 0x0F0F0F0F);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < Length; i++)
                        {
                            pData[i] = (byte)(Key ^ (16 * pData[i] | (pData[i] >> 4)));
                        }
                    }
                }
            }
            #endregion

            #region Decryption
            public void Decrypt(byte[] Data, int Length, byte[] IV)
            {
                if (Data == null)
                    throw new ArgumentNullException(nameof(Data), "Parameter cannot be null.");

                var Key = KeyGen(IV);

                XorBlock(Data, Length, IV);

                for (int i = 0; i <= 6; i++)
                {
                    if ((i % 3) != 0)
                    {
                        if ((i & 1) != 0)
                            OddDecryptTransform(Data, Length, Key);
                        else
                            EvenDecryptTransform(Data, Length, Key);
                    }
                    else
                        By3DecryptTransform(Data, Length, IV, Key);
                }
            }

            private void EvenDecryptTransform(byte[] Data, int Length, byte Key)
            {
                byte Remember = 0;
                for (int i = Length - 1; i >= 0; i--)
                {
                    byte Current = RollLeft(Data[i], ~Key % 3);
                    Current ^= 0xEC;

                    byte Tmp = Current;
                    Current ^= Remember;
                    Remember = Tmp;

                    Current -= Key;
                    Data[i] = RollRight(Current, -Key % 4);

                    Key++;
                }
            }

            private void OddDecryptTransform(byte[] Data, int Length, byte Key)
            {
                byte Remember = 0;
                for (int i = 0; i < Length; i++)
                {
                    byte Current = Data[i];
                    Current -= 0x32;
                    Current = unchecked((byte)(~Current));
                    Current = RollLeft(Current, Key % 4);

                    byte Tmp = Current;
                    Current ^= Remember;
                    Remember = Tmp;

                    Current = (byte)(16 * (Key ^ Current) | ((Key ^ Current) >> 4));
                    Data[i] = RollRight(Current, Key % 3);

                    Key--;
                }
            }

            private unsafe void By3DecryptTransform(byte[] Data, int Length, byte[] IV, byte Key)
            {
                fixed (byte* pData = Data)
                {
                    var Blocks = Length / 4;
                    var Snowflakes = Length % 4;

                    if (Blocks > 0)
                    {
                        uint* pBlocks = (uint*)(pData + Snowflakes);
                        var ReversedIV = (uint)(IV[3] | (IV[2] | (IV[1] | (IV[0]))));

                        for (int i = 0; i < Blocks; i++)
                        {
                            pBlocks[i] = (16 * ((ReversedIV ^ pBlocks[i]) & 0xFF0F0F0F)) | ((ReversedIV ^ pBlocks[i]) >> 4) & 0x0F0F0F0F;
                        }

                        if (Snowflakes > 0)
                        {
                            for (int i = 0; i < Length; i++)
                            {
                                pData[i] = (byte)(16 * (Key ^ pData[i]) | ((Key ^ pData[i]) >> 4));
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < Length; i++)
                        {
                            pData[i] = (byte)(16 * (Key ^ pData[i]) | ((Key ^ pData[i]) >> 4));
                        }
                    }
                }
            }
            #endregion

            #region Helpers
            private byte KeyGen(byte[] IV)
            {
                var x = (~IV[1] << -IV[2]) ^ (-IV[0] * ~IV[3]);
                return unchecked((byte)((x & 0xF0F0F0F) * (x & 0x0F0F0F0) / 100));
            }

            private void XorBlock(byte[] Data, int Length, byte[] IV)
            {
                bool Zig = true;
                var ivLen = IV.Length;
                for (int i = 0; i < Length; i++)
                {
                    if (i % ivLen == 0)
                        Zig = !Zig;
                    Data[i] ^= IV[Zig ? ~(i % ivLen) + ivLen : i % ivLen];
                }
            }

            private byte RollLeft(byte Value, int Count)
            {
                int Overflow = Value << (Count & 7);
                return unchecked((byte)(Overflow | (Overflow >> 8)));
            }

            private byte RollRight(byte Value, int Count)
            {
                int Overflow = Value << (8 - (Count & 7));
                return unchecked((byte)(Overflow | (Overflow >> 8)));
            }
            #endregion
        }

        /// <summary>
        /// Generates a new IV code for AES and header generation. It will reset the oldIV with the newIV automatically.
        /// </summary>
        /// <param name="pOldIV">The old IV that is used already.</param>
        private static void NextIV(byte[] pOldIV)
        {
            byte[] newIV = new byte[] { 0xF2, 0x53, 0x50, 0xC6 };
            for (var i = 0; i < 4; i++)
            {
                byte input = pOldIV[i];
                byte tableInput = sShiftKey[input];
                newIV[0] += (byte)(sShiftKey[newIV[1]] - input);
                newIV[1] -= (byte)(newIV[2] ^ tableInput);
                newIV[2] ^= (byte)(sShiftKey[newIV[3]] + input);
                newIV[3] -= (byte)(newIV[0] - tableInput);

                uint val = BitConverter.ToUInt32(newIV, 0);
                uint val2 = val >> 0x1D;
                val <<= 0x03;
                val2 |= val;
                newIV[0] = (byte)(val2 & 0xFF);
                newIV[1] = (byte)((val2 >> 8) & 0xFF);
                newIV[2] = (byte)((val2 >> 16) & 0xFF);
                newIV[3] = (byte)((val2 >> 24) & 0xFF);
            }
            Buffer.BlockCopy(newIV, 0, pOldIV, 0, 4);
        }

        private const ushort IVAN_HEADER_SIZE = 0x7FFF;
        private const int HEADER_ERROR_MORE_DATA = -1;

        /// <summary>
        /// Retrieves length of content from the header
        /// </summary>
        /// <param name="pBuffer">Buffer containing the header</param>
        /// <returns>Length of buffer</returns>
        private int GetHeaderLength(byte[] pBuffer, int pBufferLen, byte[] pIV, ushort pVersion, bool pToServer)
        {
            pVersion = Constants.MAPLE_CRYPTO_VERSION;
            ushort a = (ushort)(pBuffer[0] | (pBuffer[1] << 8));
            ushort b = (ushort)(pBuffer[2] | (pBuffer[3] << 8));
            ushort expectedIvPart = (ushort)((pIV[3] << 8) | pIV[2]);
            ushort expectedVersionPart = (ushort)(!pToServer ? pVersion : -(pVersion + 1));

            if ((a ^ expectedIvPart) != expectedVersionPart)
                throw new Exception($"Version mismatch {(a ^ expectedIvPart)} {expectedVersionPart} {(a ^ expectedVersionPart)} {expectedIvPart}");
            if ((a ^ expectedVersionPart) != expectedIvPart)
                throw new Exception($"IV mismatch {(a ^ expectedIvPart)} {expectedVersionPart} {(a ^ expectedVersionPart)} {expectedIvPart}");


            ushort len = (ushort)(a ^ b);

            if (len == IVAN_HEADER_SIZE && UseIvanPacket)
            {
                if (pBufferLen == 4) return HEADER_ERROR_MORE_DATA;
                return pBuffer[4] |
                       pBuffer[5] << 8 |
                       pBuffer[6] << 16 |
                       pBuffer[7] << 24;
            }

            return len;
        }

        /// <summary>
        /// Generates header for packets
        /// </summary>
        /// <param name="pIV">IV</param>
        /// <param name="pLength">Packet Length - Header Length</param>
        /// <param name="pVersion">MapleStory Version</param>
        /// <param name="pToServer">Is to server?</param>
        private byte[] GenerateHeader(byte[] pIV, int pLength, ushort pVersion, bool pToServer)
        {
            pVersion = Constants.MAPLE_CRYPTO_VERSION;
            ushort a = (ushort)((pIV[3] << 8) | pIV[2]);
            a ^= (ushort)(pToServer ? pVersion : -(pVersion + 1));

            byte[] headerCode;

            ushort b = a;

            if (UseIvanPacket)
            {
                b ^= IVAN_HEADER_SIZE;

                headerCode = new byte[8]
                {
                    (byte) (a & 0xFF),
                    (byte) ((a >> 8) & 0xFF),
                    (byte) (b & 0xFF),
                    (byte) ((b >> 8) & 0xFF),

                    (byte) (pLength & 0xFF),
                    (byte) ((pLength >> 8) & 0xFF),
                    (byte) ((pLength >> 16) & 0xFF),
                    (byte) ((pLength >> 24) & 0xFF),
                };
            }
            else if (pLength < IVAN_HEADER_SIZE)
            {
                b ^= (ushort)pLength;

                headerCode = new byte[4]
                {
                    (byte) (a & 0xFF),
                    (byte) ((a >> 8) & 0xFF),
                    (byte) (b & 0xFF),
                    (byte) ((b >> 8) & 0xFF),
                };
            }
            else
            {
                throw new Exception($"Sending too much data, cannot encode. Size: {pLength}");
            }

            return headerCode;
        }
        #endregion
    }
}