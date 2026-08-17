using System;
using System.Collections.Generic;

namespace Higurashi.IOS.Playback
{
    public sealed class CheckpointTimeline<T>
    {
        private readonly List<T> _items;
        private readonly int _capacity;
        private readonly bool _preserveFirst;
        private int _cursor = -1;

        public CheckpointTimeline(int capacity = 200, bool preserveFirst = false)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _preserveFirst = preserveFirst;
            _items = new List<T>(capacity);
        }

        public int Count => _items.Count;
        public int Cursor => _cursor;
        public bool CanMovePrevious => _cursor > 0;
        public bool CanMoveNext => _cursor >= 0 && _cursor < _items.Count - 1;

        public void Push(T checkpoint)
        {
            if (_cursor < _items.Count - 1)
            {
                _items.RemoveRange(_cursor + 1, _items.Count - _cursor - 1);
            }

            _items.Add(checkpoint);
            _cursor = _items.Count - 1;

            if (_items.Count <= _capacity)
            {
                return;
            }

            var overflow = _items.Count - _capacity;
            if (_preserveFirst && _items.Count > 1)
            {
                _items.RemoveRange(1, overflow);
                _cursor -= overflow;
            }
            else
            {
                _items.RemoveRange(0, overflow);
                _cursor -= overflow;
            }
        }

        public bool TryGetCurrent(out T checkpoint)
        {
            if (_cursor < 0 || _cursor >= _items.Count)
            {
                checkpoint = default;
                return false;
            }

            checkpoint = _items[_cursor];
            return true;
        }

        public bool TryMovePrevious(out T checkpoint)
        {
            if (!CanMovePrevious)
            {
                checkpoint = default;
                return false;
            }

            _cursor--;
            checkpoint = _items[_cursor];
            return true;
        }

        public bool TryMoveNext(out T checkpoint)
        {
            if (!CanMoveNext)
            {
                checkpoint = default;
                return false;
            }

            _cursor++;
            checkpoint = _items[_cursor];
            return true;
        }

        public void DiscardFuture()
        {
            if (_cursor < 0 || _cursor >= _items.Count - 1)
            {
                return;
            }

            _items.RemoveRange(_cursor + 1, _items.Count - _cursor - 1);
        }

        public T[] CopyThroughCurrent()
        {
            if (_cursor < 0)
            {
                return Array.Empty<T>();
            }

            var result = new T[_cursor + 1];
            _items.CopyTo(0, result, 0, result.Length);
            return result;
        }

        public void Clear()
        {
            _items.Clear();
            _cursor = -1;
        }
    }
}

