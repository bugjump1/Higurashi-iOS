using System.Collections.Generic;

namespace Higurashi.IOS.Playback
{
    public sealed class SceneLayerBatchTracker
    {
        private readonly HashSet<int> _preparedLayerIds = new HashSet<int>();

        public int Count => _preparedLayerIds.Count;

        public void Prepare(int layerId)
        {
            _preparedLayerIds.Add(layerId);
        }

        public int[] ConsumeForSceneChange()
        {
            var result = new int[_preparedLayerIds.Count];
            _preparedLayerIds.CopyTo(result);
            System.Array.Sort(result);
            _preparedLayerIds.Clear();
            return result;
        }

        public void Discard(int layerId)
        {
            _preparedLayerIds.Remove(layerId);
        }

        public void Commit()
        {
            _preparedLayerIds.Clear();
        }
    }
}
