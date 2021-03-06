#region usings
using System;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using NAudio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "FileStream", Category = "NAudio", Help = "Basic template with one value in/out", Tags = "")]
	#endregion PluginInfo
	public class NAudioFileStreamNode : IPluginEvaluate, IDisposable, ISampleProvider
	{
		#region fields & pins
		[Input("Play")]
		IDiffSpread<bool> FPlay;
		
		[Input("Loop")]
		IDiffSpread<bool> FLoop;
		
		[Input("Loop Start Time")]
		IDiffSpread<double> FLoopStart;
		
		[Input("Loop End Time")]
		IDiffSpread<double> FLoopEnd;
		
		[Input("Do Seek", IsBang = true)]
		IDiffSpread<bool> FDoSeek;
		
		[Input("Seek Time")]
		IDiffSpread<double> FSeekPosition;		
		
		[Input("Volume", DefaultValue = 0.25f)] //What's the best default? Good to have something audible but probably good to avoid max to save ears/speakers
		IDiffSpread<float> FVolume;
		
		[Input("Filename", StringType = StringType.Filename, FileMask="Audio File (*.wav, *.mp3, *.aiff)|*.wav;*.mp3;*.aiff")]
		IDiffSpread<string> FFilename;

		[Output("Output")]
		ISpread<ISampleProvider> FOutput;
		
		[Output("Duration")]
		ISpread<double> FDuration;
		
		[Output("Position")]
		ISpread<double> FPosition;
			
		[Output("Can Seek")]
		ISpread<bool> FCanSeek;
		
		private bool FIsPlaying = false;
		private bool FRunToEndBeforeLooping = true;
		private TimeSpan FLoopStartTime;
		private TimeSpan FLoopEndTime;
		private TimeSpan FSeekTime;

		[Import()]
		ILogger FLogger;
		#endregion fields & pins
		
		AudioFileReader FAudioFile;
		SilenceProvider FSilence;
		//ISampleProvider FOut;

		public NAudioFileStreamNode()
		{
			FSilence = new SilenceProvider(this.WaveFormat);
		}
		
		public void Dispose()
		{
			
		}
		
		public void OpenFile()
		{
			FAudioFile = new AudioFileReader(FFilename[0]);
		}
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if(FFilename.IsChanged)
			{
				OpenFile();
				if(FAudioFile != null)
				{
					FDuration[0] = FAudioFile.TotalTime.TotalSeconds;
					FCanSeek[0] = FAudioFile.CanSeek;
				}
				else
				{
					FDuration[0] = 0;
					FCanSeek[0] = FAudioFile.CanSeek;
				}			
			}
			
			if(FAudioFile == null)
			{
				FOutput[0]=null;
				FPosition[0] = 0;
				FDuration[0] = 0;
	
			}
			else
			{
				if(FOutput[0] != this)
				{
					FOutput[0] = this;
	
				}				
				if(FVolume.IsChanged)
				{
					FAudioFile.Volume = FVolume[0];
				}
				FPosition[0] = FAudioFile.CurrentTime.TotalSeconds;			
			}
			if(FPlay.IsChanged)
			{
				FIsPlaying = FPlay[0];
				if((FAudioFile.CurrentTime >= FAudioFile.TotalTime) && FPlay[0])
				{
					FAudioFile.Position = 0;
				}
			}
			if(FLoop.IsChanged)
			{
				if(FLoop[0] && FAudioFile.CurrentTime <= FLoopEndTime)
				{
					FRunToEndBeforeLooping = false;
				}
				else if (FLoop[0])
				{
					FRunToEndBeforeLooping = true;
				}
			}
			if(FLoopStart.IsChanged)
			{
				FLoopStartTime = TimeSpan.FromSeconds(FLoopStart[0]);
			}			
			if(FLoopEnd.IsChanged)
			{
				FLoopEndTime = TimeSpan.FromSeconds(Math.Min(FLoopEnd[0], FAudioFile.TotalTime.TotalSeconds));
			}
			if( FLoop[0] && !FRunToEndBeforeLooping)
			{
				if(FAudioFile.CurrentTime > FLoopEndTime)
				{
					FAudioFile.CurrentTime = FLoopStartTime;
				}
			}			
			if(FSeekPosition.IsChanged)
			{
				FSeekTime = TimeSpan.FromSeconds(Math.Min(FAudioFile.TotalTime.TotalSeconds, FSeekPosition[0]));
			}
			if(FDoSeek.IsChanged && FDoSeek[0] && FAudioFile.CanSeek)
			{
				if(!FIsPlaying && FPlay[0]) FIsPlaying = true;
				if(FSeekTime > FLoopEndTime) FRunToEndBeforeLooping = true;
				if(FSeekTime < FLoopStartTime) FRunToEndBeforeLooping = false;
				FAudioFile.CurrentTime = FSeekTime;
			}

		}
		
		
		public WaveFormat WaveFormat
		{
			//get{ return FAudioFile.WaveFormat; }
			get{ return WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); }
		}
		
		public unsafe int Read(float[] buffer, int offset, int sampleCount)
		{
			//return FAudioFile.Read(buffer, offset, sampleCount);				
			
            int bytesread = 0;
			if(FIsPlaying)
			{
	            bytesread = FAudioFile.Read(buffer, offset, sampleCount);
	
	            if (bytesread == 0)
	            {
	            	if(FLoop[0])
	            	{
	            		FAudioFile.CurrentTime = FLoopStartTime;
	            		FRunToEndBeforeLooping = false;
		                bytesread = FAudioFile.Read(buffer, offset, sampleCount);	            		
	            	}
	            	else
	            	{
	            		FIsPlaying = false;
	            		bytesread = FSilence.Read(buffer, offset, sampleCount);
	            	}

	            }				
			}
			else
			{
				bytesread = FSilence.Read(buffer, offset, sampleCount);
			}
			

            return bytesread;			
		}			
	}
	
	//SilenceProvider code from vexorum, http://naudio.codeplex.com/workitem/16377
	public class SilenceProvider : ISampleProvider
	{
	    private readonly WaveFormat format;
	
	    public SilenceProvider(WaveFormat format)
	    {
	        this.format = format;
	    }
	
	    public int Read(float[] buffer, int offset, int count)
	    {
	        for (int i = 0; i < count; i++)
	        {
	            buffer[offset + i] = 0;
	        }
	        return count;
	    }
	
	    public WaveFormat WaveFormat
	    {
	        get { return format; }
	    }
	}	
}
