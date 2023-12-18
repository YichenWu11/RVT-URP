using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace RuntimeVirtualTexture
{
    [BurstCompile]
    internal struct FeedbackAnalysisTask : IJob
    {
        internal int start;
        internal int end;

        [ReadOnly]
        internal NativeArray<uint> data;

        internal UnsafeHashSet<uint> requests;

        public void Execute()
        {
            uint lastPixel = 0x0;
            for (var i = start; i < end; i++)
            {
                if (data[i] == lastPixel)
                {
                    continue;
                }

                if (lastPixel != 0x0)
                {
                    requests.Add(lastPixel);
                }

                lastPixel = data[i];
            }
        }
    }
}