using System;

namespace SysWeaver.MicroService
{
    public sealed class PassKeyAuth
    {
        public String CredentialId;
        public Byte[] Challenge;
    }

    public sealed class PassKeyAttach
    {
        public Byte[] Challenge;
        public String CredentialId;
        public String DeviceName;
        public Byte[] RawId;
        public Byte[] AttestationData;
        public Byte[] PublicKey;
        public int PublicKeyAlgorithm;
        public String[] Transports;
    }

}