using System;
using Assistant;

namespace Assistant.Filters
{
    public class SoundFilter : Filter
    {
        public static void Initialize()
        {
            Filter.Register(new SoundFilter(LocString.BardMusic, 0x38, 0x39, 0x43, 0x44, 0x45, 0x46, 0x4C, 0x4D, 0x52,
                0x53));
            Filter.Register(new SoundFilter(LocString.DogSounds, GetRange(0x85, 0x89)));
            Filter.Register(new SoundFilter(LocString.CatSounds, GetRange(0x69, 0x6D)));
            Filter.Register(new SoundFilter(LocString.HorseSounds, GetRange(0xA8, 0xAC)));
            Filter.Register(new SoundFilter(LocString.SheepSounds, GetRange(0xD6, 0xDA)));
            Filter.Register(new SoundFilter(LocString.SS_Sound, 0x24A));
            Filter.Register(new SoundFilter(LocString.FizzleSound, 0x5C));
            Filter.Register(new SoundFilter(LocString.PackSound, 0x48));
            Filter.Register(new SoundFilter(LocString.DeerSounds, 0x82, 0x83, 0x84, 0x85, 0x2BE, 0x2BF, 0x2C0, 0x4CB, 0x4CC));
            Filter.Register(new SoundFilter(LocString.CyclopTitanSounds, 0x25D, 0x25E, 0x25F, 0x260, 0x261, 0x262, 0x263, 0x264, 0x265, 0x266));
        }

        public static ushort[] GetRange(ushort min, ushort max)
        {
            if (max < min)
                return new ushort[0];

            ushort[] range = new ushort[max - min + 1];
            for (ushort i = min; i <= max; i++)
                range[i - min] = i;
            return range;
        }

        private LocString m_Name;
        private ushort[] m_Sounds;

        private SoundFilter(LocString name, params ushort[] blockSounds)
        {
            m_Name = name;
            m_Sounds = blockSounds;
        }

        public override byte[] PacketIDs
        {
            get { return new byte[] {0x54}; }
        }

        public override LocString Name
        {
            get { return m_Name; }
        }

        public override void OnFilter(PacketReader p, PacketHandlerEventArgs args)
        {
            p.ReadByte(); // flags

            ushort sound = p.ReadUInt16();
            for (int i = 0; i < m_Sounds.Length; i++)
            {
                if (m_Sounds[i] == sound)
                {
                    args.Block = true;
                    return;
                }
            }
        }
    }
}