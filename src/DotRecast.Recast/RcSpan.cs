/*
Copyright (c) 2009-2010 Mikko Mononen memon@inside.org
recast4j copyright (c) 2015-2019 Piotr Piastucki piotr@jtilia.org
DotRecast Copyright (c) 2023 Choi Ikpil ikpil@naver.com

This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.
Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:
1. The origin of this software must not be misrepresented; you must not
 claim that you wrote the original software. If you use this software
 in a product, an acknowledgment in the product documentation would be
 appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
 misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Collections.Concurrent;

namespace DotRecast.Recast
{
    /** Represents a span in a heightfield. */
    public struct RcSpan
    {
        /** The lower limit of the span. [Limit: &lt; smax] */
        public int smin;

        /** The upper limit of the span. [Limit: &lt;= SPAN_MAX_HEIGHT] */
        public int smax;

        /** The area id assigned to the span. */
        public int area;

        /** The index next span higher up in column. For span in the free list, this is the index of the next free span. */
        public uint next;
    }

    /** A pool of spans. Index 0 is reserved to mean 'null'. */
    // Storage is split into fixed-size pages: growing adds a page (nothing is copied, indices never move).
    public class RcSpanPool
    {
        public const int PAGE_SHIFT = 16;
        public const int PAGE_SIZE = 1 << PAGE_SHIFT;
        private const uint PAGE_MASK = PAGE_SIZE - 1;

        private readonly RcSpanPageSource pageSource; // null: pages are just allocated and dropped with the pool
        private RcSpan[][] pages = new RcSpan[16][];
        private int pageCount;
        private uint firstUnalloc = 1; // storage element with index >= this is not initialized

        public RcSpanPool() : this(null)
        {
        }

        public RcSpanPool(RcSpanPageSource pageSource)
        {
            this.pageSource = pageSource;
            AddPage();
            pages[0][0].next = firstUnalloc;
        }

        public ref RcSpan Span(uint index) => ref pages[index >> PAGE_SHIFT][index & PAGE_MASK];

        public uint Alloc()
        {
            ref RcSpan head = ref pages[0][0];
            var index = head.next;
            if (index < firstUnalloc)
            {
                // get span from the free list
                ref RcSpan span = ref Span(index);
                head.next = span.next;
                span.next = 0;
                return index;
            }

            // free list is empty; if storage is full, add a page (page 0 stays where it is, so `head` stays valid)
            if (index == (uint)pageCount << PAGE_SHIFT)
                AddPage();

            // index == firstUnalloc => storage[index] == 0 (never initialized)
            head.next = ++firstUnalloc;
            return index;
        }

        public void Free(uint index)
        {
            // push span to the head of the free list
            ref RcSpan head = ref pages[0][0];
            Span(index).next = head.next;
            head.next = index;
        }

        // Zeroes the used part and gives the pages back to the page source; this pool must not be used afterwards.
        public void ReleasePages()
        {
            for (int i = 0; i < pageCount; ++i)
            {
                long used = Math.Min((long)firstUnalloc - ((long)i << PAGE_SHIFT), PAGE_SIZE);
                if (used > 0)
                    Array.Clear(pages[i], 0, (int)used);
                pageSource?.Return(pages[i]);
                pages[i] = null;
            }

            pageCount = 0;
        }

        private void AddPage()
        {
            if (pageCount == pages.Length)
                Array.Resize(ref pages, pages.Length * 2);
            pages[pageCount++] = pageSource != null ? pageSource.Take() : new RcSpan[PAGE_SIZE];
        }
    }

    // Thread-safe stock of zeroed span pages shared by heightfields that are built one after another or concurrently.
    public sealed class RcSpanPageSource
    {
        private readonly ConcurrentBag<RcSpan[]> pages = new ConcurrentBag<RcSpan[]>();

        internal RcSpan[] Take() => pages.TryTake(out var page) ? page : new RcSpan[RcSpanPool.PAGE_SIZE];

        internal void Return(RcSpan[] page) => pages.Add(page);

        public void Clear()
        {
            while (pages.TryTake(out _))
            {
            }
        }
    }
}
