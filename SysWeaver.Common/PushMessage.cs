using System;


namespace SysWeaver
{

    public class PushMessage
    {
#if DEBUG
        public override string ToString() => String.Concat(this.GetType().Name, " \"", Type, '"');
#endif//DEBUG

        public String Type;

        public PushMessage()
        {
        }

        public PushMessage(string type)
        {
            Type = type;
        }
    }

    public class PushMessageStringValue : PushMessage
    {
#if DEBUG
        public override string ToString() => String.Join(" = ", base.ToString(), Value);
#endif//DEBUG


        public String Value;

        public PushMessageStringValue(string type, string value)
        {
            Type = type;
            Value = value;
        }
    }

    public class PushMessagIntValue : PushMessage
    {
#if DEBUG
        public override string ToString() => String.Join(" = ", base.ToString(), Value);
#endif//DEBUG


        public long Value;

        public PushMessagIntValue(string type, long value)
        {
            Type = type;
            Value = value;
        }
    }

    public class PushMessagNumberValue: PushMessage
    {
#if DEBUG
        public override string ToString() => String.Join(" = ", base.ToString(), Value);
#endif//DEBUG


        public double Value;

        public PushMessagNumberValue(string type, double value)
        {
            Type = type;
            Value = value;
        }
    }

}
