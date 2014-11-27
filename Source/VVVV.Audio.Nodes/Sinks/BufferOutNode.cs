﻿#region usings
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Audio;


using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	
	
	[PluginInfo(Name = "GetBuffer", Category = "VAudio", Version = "Sink", Help = "Returns a complete buffer", Tags = "Scope, Samples")]
    public class BufferOutNode : GenericAudioSinkNode<BufferOutSignal>
	{
		
		[Output("Buffer")]
		public ISpread<ISpread<float>> FBufferOut;

        protected override void SetOutputs(int i, BufferOutSignal instance)
        {
            if (instance != null)
            {
                var spread = FBufferOut[i];
                float[] buffer = instance.BufferOut;
                if (buffer != null)
                {
                    if (spread == null)
                    {
                        spread = new Spread<float>(buffer.Length);
                    }
                    spread.SliceCount = buffer.Length;
                    spread.AssignFrom(buffer);
                }
            }
            else
            {
                FBufferOut[i].SliceCount = 0;
            }
        }

        protected override void SetOutputSliceCount(int sliceCount)
        {
            FBufferOut.SliceCount = sliceCount;
        }

        protected override BufferOutSignal GetInstance(int i)
        {
            return new BufferOutSignal(FInputs[i]);
        }

        protected override void SetParameters(int i, BufferOutSignal instance)
        {
            instance.Input = FInputs[i];
        }
    }
}


