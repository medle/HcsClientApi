﻿using System;

namespace GostCryptography.Asn1.Ber
{
	[Serializable]
	public class Asn1VisibleString : Asn18BitCharString
	{
		public static readonly Asn1Tag Tag = new Asn1Tag(0, 0, VisibleStringTypeCode);

		public Asn1VisibleString()
			: base(VisibleStringTypeCode)
		{
		}

		public Asn1VisibleString(string data)
			: base(data, VisibleStringTypeCode)
		{
		}

		public override void Decode(Asn1BerDecodeBuffer buffer, bool explicitTagging, int implicitLength)
		{
			Decode(buffer, explicitTagging, implicitLength, Tag);
		}

		public override int Encode(Asn1BerEncodeBuffer buffer, bool explicitTagging)
		{
			return Encode(buffer, explicitTagging, Tag);
		}

		public override void Encode(Asn1BerOutputStream outs, bool explicitTagging)
		{
			outs.EncodeCharString(Value, explicitTagging, Tag);
		}
	}
}