﻿using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO.Operations.SubDocument
{
    internal abstract class SubDocSingularMutationBase<T> : SubDocSingularBase<T>
    {
        protected SubDocSingularMutationBase(ISubDocBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(builder, key, vBucket, transcoder, opaque, timeout)
        {
        }

        protected SubDocSingularMutationBase(ISubDocBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, key, vBucket, transcoder, timeout)
        {
        }

        public override void WriteHeader(byte[] buffer)
        {
            Converter.FromByte((byte)Magic.Request, buffer, HeaderIndexFor.Magic);//0
            Converter.FromByte((byte)OperationCode, buffer, HeaderIndexFor.Opcode);//1
            Converter.FromInt16(KeyLength, buffer, HeaderIndexFor.KeyLength);//2-3
            Converter.FromByte((byte)ExtrasLength, buffer, HeaderIndexFor.ExtrasLength);  //4
            //5 datatype?
            if (VBucket != null)
            {
                Converter.FromInt16((short)VBucket.Index, buffer, HeaderIndexFor.VBucket);//6-7
            }

            Converter.FromInt32(ExtrasLength + KeyLength + BodyLength + PathLength, buffer, HeaderIndexFor.BodyLength);//8-11
            Converter.FromUInt32(Opaque, buffer, HeaderIndexFor.Opaque);//12-15
            Converter.FromUInt64(Cas, buffer, HeaderIndexFor.Cas);
        }

        public override byte[] Write()
        {
            var totalLength = HeaderLength + KeyLength + ExtrasLength + PathLength + BodyLength;
            var buffer = AllocateBuffer(totalLength);

            WriteHeader(buffer);
            WriteExtras(buffer, 24);
            WriteKey(buffer, HeaderLength + ExtrasLength);
            WritePath(buffer, HeaderLength + ExtrasLength + KeyLength);
            WriteBody(buffer, HeaderLength + ExtrasLength + KeyLength + PathLength);

            return buffer;
        }

        public override void WriteExtras(byte[] buffer, int offset)
        {
            Converter.FromInt16(PathLength, buffer, offset); //2@24 Path length
            Converter.FromByte((byte) CurrentSpec.PathFlags, buffer, offset + 2); //1@26 PathFlags

            var hasExpiry = Expires > 0;
            if (hasExpiry)
            {
                Converter.FromUInt32(Expires, buffer, offset + 3); //4@27 Expiration time (if present, extras is 7)
            }
            if (CurrentSpec.DocFlags != SubdocDocFlags.None)
            {
                // write doc flags, offset depends on if there is an expiry
                Converter.FromByte((byte) CurrentSpec.DocFlags, buffer, offset + (hasExpiry ? 7 : 3));
            }
        }

        public override byte[] CreateBody()
        {
            var bytes = Transcoder.Serializer.Serialize(CurrentSpec.Value);
            if (CurrentSpec.RemoveBrackets)
            {
                return bytes.StripBrackets();
            }
            return bytes;
        }

        public override void ReadExtras(byte[] buffer)
        {
            if (buffer.Length >= 40 && VBucket != null)
            {
                var uuid = Converter.ToInt64(buffer, 24);
                var seqno = Converter.ToInt64(buffer, 32);
                MutationToken = new MutationToken(VBucket.BucketName, (short)VBucket.Index, uuid, seqno);
            }
        }
    }
}
