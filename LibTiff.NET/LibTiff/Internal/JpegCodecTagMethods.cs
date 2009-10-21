﻿/* Copyright (C) 2008-2009, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * This software is based in part on the work of the Sam Leffler, Silicon 
 * Graphics, Inc. and contributors.
 *
 * Copyright (c) 1988-1997 Sam Leffler
 * Copyright (c) 1991-1997 Silicon Graphics, Inc.
 * For conditions of distribution and use, see the accompanying README file.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace BitMiracle.LibTiff.Internal
{
    class JpegCodecTagMethods : TiffTagMethods
    {
        public override bool vsetfield(Tiff tif, TIFFTAG tag, params object[] ap)
        {
            JpegCodec sp = tif.m_currentCodec as JpegCodec;
            Debug.Assert(sp != null);
            
            switch (tag)
            {
                case TIFFTAG.TIFFTAG_JPEGTABLES:
                    int v32 = (int)ap[0];
                    if (v32 == 0)
                    {
                        /* XXX */
                        return false;
                    }

                    sp.m_jpegtables = new byte [v32];
                    Array.Copy(ap[1] as byte[], sp.m_jpegtables, v32);
                    sp.m_jpegtables_length = v32;
                    tif.setFieldBit(JpegCodec.FIELD_JPEGTABLES);
                    break;

                case TIFFTAG.TIFFTAG_JPEGQUALITY:
                    sp.m_jpegquality = (int)ap[0];
                    return true; /* pseudo tag */

                case TIFFTAG.TIFFTAG_JPEGCOLORMODE:
                    sp.m_jpegcolormode = (JPEGCOLORMODE)ap[0];
                    sp.JPEGResetUpsampled();
                    return true; /* pseudo tag */

                case TIFFTAG.TIFFTAG_PHOTOMETRIC:
                    bool ret_value = base.vsetfield(tif, tag, ap);
                    sp.JPEGResetUpsampled();
                    return ret_value;

                case TIFFTAG.TIFFTAG_JPEGTABLESMODE:
                    sp.m_jpegtablesmode = (JPEGTABLESMODE)ap[0];
                    return true; /* pseudo tag */
                
                case TIFFTAG.TIFFTAG_YCBCRSUBSAMPLING:
                    /* mark the fact that we have a real ycbcrsubsampling! */
                    sp.m_ycbcrsampling_fetched = true;
                    /* should we be recomputing upsampling info here? */
                    return base.vsetfield(tif, tag, ap);
                
                case TIFFTAG.TIFFTAG_FAXRECVPARAMS:
                    sp.m_recvparams = (uint)ap[0];
                    break;
                
                case TIFFTAG.TIFFTAG_FAXSUBADDRESS:
                    Tiff.setString(out sp.m_subaddress, ap[0] as string);
                    break;
                
                case TIFFTAG.TIFFTAG_FAXRECVTIME:
                    sp.m_recvtime = (uint)ap[0];
                    break;
                
                case TIFFTAG.TIFFTAG_FAXDCS:
                    Tiff.setString(out sp.m_faxdcs, ap[0] as string);
                    break;
                
                default:
                    return base.vsetfield(tif, tag, ap);
            }

            TiffFieldInfo fip = tif.FieldWithTag(tag);
            if (fip != null)
                tif.setFieldBit(fip.field_bit);
            else
                return false;

            tif.m_flags |= Tiff.TIFF_DIRTYDIRECT;
            return true;
        }

        public override object[] vgetfield(Tiff tif, TIFFTAG tag)
        {
            JpegCodec sp = tif.m_currentCodec as JpegCodec;
            Debug.Assert(sp != null);

            object[] result = null;

            switch (tag)
            {
                case TIFFTAG.TIFFTAG_JPEGTABLES:
                    result = new object[2];
                    result[0] = sp.m_jpegtables_length;
                    result[1] = sp.m_jpegtables;
                    break;

                case TIFFTAG.TIFFTAG_JPEGQUALITY:
                    result = new object[1];
                    result[0] = sp.m_jpegquality;
                    break;

                case TIFFTAG.TIFFTAG_JPEGCOLORMODE:
                    result = new object[1];
                    result[0] = sp.m_jpegcolormode;
                    break;

                case TIFFTAG.TIFFTAG_JPEGTABLESMODE:
                    result = new object[1];
                    result[0] = sp.m_jpegtablesmode;
                    break;

                case TIFFTAG.TIFFTAG_YCBCRSUBSAMPLING:
                    JPEGFixupTestSubsampling(tif);
                    return base.vgetfield(tif, tag);

                case TIFFTAG.TIFFTAG_FAXRECVPARAMS:
                    result = new object[1];
                    result[0] = sp.m_recvparams;
                    break;

                case TIFFTAG.TIFFTAG_FAXSUBADDRESS:
                    result = new object[1];
                    result[0] = sp.m_subaddress;
                    break;

                case TIFFTAG.TIFFTAG_FAXRECVTIME:
                    result = new object[1];
                    result[0] = sp.m_recvtime;
                    break;

                case TIFFTAG.TIFFTAG_FAXDCS:
                    result = new object[1];
                    result[0] = sp.m_faxdcs;
                    break;

                default:
                    return base.vgetfield(tif, tag);
            }

            return result;
        }

        public override void printdir(Tiff tif, Stream fd, TiffPrintDirectoryFlags flags)
        {
            JpegCodec sp = tif.m_currentCodec as JpegCodec;
            Debug.Assert(sp != null);
            
            if (tif.fieldSet(JpegCodec.FIELD_JPEGTABLES))
                Tiff.fprintf(fd, "  JPEG Tables: (%lu bytes)\n", sp.m_jpegtables_length);
            
            if (tif.fieldSet(JpegCodec.FIELD_RECVPARAMS))
                Tiff.fprintf(fd, "  Fax Receive Parameters: %08lx\n", sp.m_recvparams);
            
            if (tif.fieldSet(JpegCodec.FIELD_SUBADDRESS))
                Tiff.fprintf(fd, "  Fax SubAddress: %s\n", sp.m_subaddress);
            
            if (tif.fieldSet(JpegCodec.FIELD_RECVTIME))
                Tiff.fprintf(fd, "  Fax Receive Time: %lu secs\n", sp.m_recvtime);
            
            if (tif.fieldSet(JpegCodec.FIELD_FAXDCS))
                Tiff.fprintf(fd, "  Fax DCS: %s\n", sp.m_faxdcs);
        }

        /*
        * Some JPEG-in-TIFF produces do not emit the YCBCRSUBSAMPLING values in
        * the TIFF tags, but still use non-default (2,2) values within the jpeg
        * data stream itself.  In order for TIFF applications to work properly
        * - for instance to get the strip buffer size right - it is imperative
        * that the subsampling be available before we start reading the image
        * data normally.  This function will attempt to load the first strip in
        * order to get the sampling values from the jpeg data stream.  Various
        * hacks are various places are done to ensure this function gets called
        * before the td_ycbcrsubsampling values are used from the directory structure,
        * including calling TIFFGetField() for the YCBCRSUBSAMPLING field from 
        * TIFFStripSize(), and the printing code in tif_print.c. 
        *
        * Note that JPEGPreDeocode() will produce a fairly loud warning when the
        * discovered sampling does not match the default sampling (2,2) or whatever
        * was actually in the tiff tags. 
        *
        * Problems:
        *  o This code will cause one whole strip/tile of compressed data to be
        *    loaded just to get the tags right, even if the imagery is never read.
        *    It would be more efficient to just load a bit of the header, and
        *    initialize things from that. 
        *
        * See the bug in bugzilla for details:
        *
        * http://bugzilla.remotesensing.org/show_bug.cgi?id=168
        *
        * Frank Warmerdam, July 2002
        */
        private void JPEGFixupTestSubsampling(Tiff tif)
        {
            if (Tiff.CHECK_JPEG_YCBCR_SUBSAMPLING)
            {
                JpegCodec sp = tif.m_currentCodec as JpegCodec;
                sp.InitializeLibJPEG(false, false);

                /*
                * Some JPEG-in-TIFF files don't provide the ycbcrsampling tags, 
                * and use a sampling schema other than the default 2,2.  To handle
                * this we actually have to scan the header of a strip or tile of
                * jpeg data to get the sampling.  
                */
                if (!sp.m_common.m_is_decompressor || sp.m_ycbcrsampling_fetched || tif.m_dir.td_photometric != PHOTOMETRIC.PHOTOMETRIC_YCBCR)
                    return;

                sp.m_ycbcrsampling_fetched = true;
                if (tif.IsTiled())
                {
                    if (!tif.fillTile(0))
                        return;
                }
                else
                {
                    if (!tif.fillStrip(0))
                        return;
                }

                tif.SetField(TIFFTAG.TIFFTAG_YCBCRSUBSAMPLING, sp.m_h_sampling, sp.m_v_sampling);
            }
        }
    }
}
