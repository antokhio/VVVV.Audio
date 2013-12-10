﻿#region usings
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Linq.Expressions;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Audio;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;
using NAudio.Utils;
using VVVV.Core.Logging;

#endregion usings

namespace VVVV.Nodes
{
	public class SineTable
	{
		public double[] Table;
		private double StepSize;
		private int Size;
		public SineTable(int size)
		{
			Size = size;
			Table = new double[Size];
			StepSize = (1.0f/Size) * (Math.PI * 2);
			FillTable();
		}
		
		void FillTable()
		{
			for (int i = 0; i < Size; i++)
			{
				Table[i] = Math.Sin(( (i / (double)Size) * (Math.PI * 2)));
			}
		}
		
		public double Sin(float phase)
		{
			var index = (int)Math.Floor(phase / StepSize) % Size;
			
			return Table[index];
		}
		
		public double SinInterpolated(float phase)
		{
			var floatIndex = phase / StepSize;
			var index = (int)Math.Floor(floatIndex) % Size;
			var nextIndex = (index + 1) % Size;
			
			return VMath.Lerp(Table[index], Table[nextIndex], floatIndex % 1.0);
		}
	}
	
	public class MultiSineSignal : AudioSignal
	{
		SineTable SineTable = new SineTable(2205);
		public MultiSineSignal(ISpread<float> frequency, ISpread<float> gain)
			: base(44100)
		{
			Frequencies = frequency;
			Gains = gain;
			Phases = new Spread<float>();
		}
		
		public ISpread<float> Frequencies;
		public ISpread<float> Gains;
		private readonly float TwoPi = (float)(Math.PI * 2);
		private ISpread<float> Phases;
		
		protected override void FillBuffer(float[] buffer, int offset, int count)
		{
			PerfCounter.Start("MultiSine");
			var sampleRate = this.WaveFormat.SampleRate;
			var spreadMax = Frequencies.CombineWith(Gains);
			Phases.Resize(spreadMax, () => default(float), f => f = 0);
			for (int slice = 0; slice < spreadMax; slice++) 
			{
			 	var increment = TwoPi*Frequencies[slice]/sampleRate;
			 	var gain = Gains[slice];
			 	var phase = Phases[slice];
			 	
			 	if(slice == 0)
			 	{
			 		for (int i = 0; i < count; i++)
			 		{
			 			// Sinus Generator
			 			buffer[i] = gain*(float)Math.Sin(phase);
			 			
			 			phase += increment;
			 			if(phase > TwoPi)
			 				phase -= TwoPi;
			 			else if(phase < 0)
			 				phase += TwoPi;
			 		}
			 	}
			 	else
			 	{
			 		for (int i = 0; i < count; i++)
			 		{
			 			// Sinus Generator
			 			buffer[i] += gain*(float)Math.Sin(phase);
			 			
			 			phase += increment;
			 			if(phase > TwoPi)
			 				phase -= TwoPi;
			 			else if(phase < 0)
			 				phase += TwoPi;
			 		}
			 	}
			 		
				
				Phases[slice] = phase; //write back
			}
			
			PerfCounter.Stop("MultiSine");
		}
			
	}
	
	
	public class SineSignal : AudioSignal
	{
		public SineSignal(float frequency, float gain)
			: base(44100)
		{
			Frequency = frequency;
			Gain = gain;
		}
		
		public float Frequency;
		public float Gain = 0.1f;
		private float TwoPi = (float)(Math.PI * 2);
		private float phase = 0;
		
		protected override void FillBuffer(float[] buffer, int offset, int count)
		{
			PerfCounter.Start("Sine");
			
			var sampleRate = this.WaveFormat.SampleRate;
			var increment = TwoPi*Frequency/sampleRate;
			for (int i = 0; i < count; i++)
			{
				// Sinus Generator
				buffer[i] = Gain*(float)Math.Sin(phase);
				
				phase += increment;
				if(phase > TwoPi)
					phase -= TwoPi;
				else if(phase < 0)
					phase += TwoPi;
			}
			
			PerfCounter.Stop("Sine");
		}
	}
	
	[PluginInfo(Name = "Sine", Category = "Audio", Version = "Source", Help = "Creates a sine wave", AutoEvaluate = true, Tags = "Wave")]
	public class SineSignalNode : GenericAudioSourceNode<SineSignal>
	{
		[Input("Frequency", DefaultValue = 440)]
		IDiffSpread<float> Frequency;
		
		[Input("Gain", DefaultValue = 0.1)]
		IDiffSpread<float> Gain;
		
		protected override void SetParameters(int i, SineSignal instance)
		{
			instance.Gain = Gain[i];
			instance.Frequency = Frequency[i];
		}
		
		protected override AudioSignal GetInstance(int i)
		{
			return new SineSignal(Frequency[i], Gain[i]);
		}
	}
	
	
	[PluginInfo(Name = "MultiSine", Category = "Audio", Version = "Source", Help = "Creates a spread of sine waves", AutoEvaluate = true, Tags = "LFO, additive, synthesis")]
	public class MultiSineSignalNode : GenericAudioSourceNode<MultiSineSignal>
	{
		[Input("Frequency", DefaultValue = 440)]
		IDiffSpread<ISpread<float>> Frequency;
		
		[Input("Gain", DefaultValue = 0.1)]
		IDiffSpread<ISpread<float>> Gain;
		
		protected override int GetSpreadMax(int originalSpreadMax)
		{
			return Math.Max(Frequency.SliceCount, Gain.SliceCount);
		}
		
		protected override void SetParameters(int i, MultiSineSignal instance)
		{
			instance.Gains = Gain[i];
			instance.Frequencies = Frequency[i];
		}
		
		protected override AudioSignal GetInstance(int i)
		{
			return new MultiSineSignal(Frequency[i], Gain[i]);
		}
	}
}

