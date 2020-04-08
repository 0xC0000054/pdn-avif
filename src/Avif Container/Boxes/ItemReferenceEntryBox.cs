////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace AvifFileType.AvifContainer
{
    internal sealed class ItemReferenceEntryBox
        : Box, IItemReferenceEntry
    {
        private readonly List<uint> toItemIds;
        private ItemReferenceBox parent;

        public ItemReferenceEntryBox(EndianBinaryReader reader, ItemReferenceBox parent)
            : base(reader)
        {

            switch (parent.Version)
            {
                case 0:
                    this.FromItemId = reader.ReadUInt16();
                    break;
                case 1:
                    this.FromItemId = reader.ReadUInt32();
                    break;
                default:
                    throw new FormatException($"ItemReferenceBox version must be 0 or 1, actual value { parent.Version }");
            }

            ushort itemCount = reader.ReadUInt16();
            this.toItemIds = new List<uint>(itemCount);

            for (int i = 0; i < itemCount; i++)
            {
                uint toItemId;

                switch (parent.Version)
                {
                    case 0:
                        toItemId = reader.ReadUInt16();
                        break;
                    case 1:
                        toItemId = reader.ReadUInt32();
                        break;
                    default:
                        throw new FormatException($"ItemReferenceBox version must be 0 or 1, actual value { parent.Version }");
                }

                this.toItemIds.Add(toItemId);
            }

            this.parent = parent;
        }

        public uint FromItemId { get; }

        public IReadOnlyList<uint> ToItemIds => this.toItemIds;

        public ItemReferenceEntryBox(uint fromItemId, FourCC referenceType, params uint[] parentItemIds)
            : base(referenceType)
        {
            if (parentItemIds is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(parentItemIds));
            }

            this.FromItemId = fromItemId;
            this.toItemIds = new List<uint>(parentItemIds);
        }

        /// <summary>
        /// Sets the parent <see cref="ItemReferenceBox"/> of this item.
        /// </summary>
        /// <param name="parent">The parent.</param>
        public void SetParent(ItemReferenceBox parent)
        {
            if (parent == null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(parent));
            }

            if (this.parent != null)
            {
                ExceptionUtil.ThrowInvalidOperationException($"The { nameof(ItemReferenceEntryBox) } already has a parent.");
            }

            this.parent = parent;
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            switch (this.parent.Version)
            {
                case 0:
                    writer.Write((ushort)this.FromItemId);
                    break;
                case 1:
                    writer.Write(this.FromItemId);
                    break;
                default:
                    throw new FormatException($"ItemReferenceBox version must be 0 or 1, actual value { this.parent.Version }");
            }

            writer.Write((ushort)this.toItemIds.Count);

            for (int i = 0; i < this.toItemIds.Count; i++)
            {
                switch (this.parent.Version)
                {
                    case 0:
                        writer.Write((ushort)this.toItemIds[i]);
                        break;
                    case 1:
                        writer.Write(this.toItemIds[i]);
                        break;
                    default:
                        throw new FormatException($"ItemReferenceBox version must be 0 or 1, actual value { this.parent.Version }");
                }
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            ulong fromItemIdSize;
            ulong toItemIdSize;

            switch (this.parent.Version)
            {
                case 0:
                    fromItemIdSize = sizeof(ushort);
                    toItemIdSize = sizeof(ushort);
                    break;
                case 1:
                    fromItemIdSize = sizeof(uint);
                    toItemIdSize = sizeof(uint);
                    break;
                default:
                    throw new FormatException($"ItemReferenceBox version must be 0 or 1, actual value { this.parent.Version }");
            }

            return base.GetTotalBoxSize()
                   + fromItemIdSize // From item id
                   + sizeof(ushort) // To item id count
                   + ((ulong)this.toItemIds.Count * toItemIdSize);
        }
    }
}
