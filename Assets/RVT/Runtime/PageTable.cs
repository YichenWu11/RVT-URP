using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace RuntimeVirtualTexture
{
    public struct PageItem
    {
        public uint PageId; // mip(8) | pageX(12) | pageY (12)
        public uint TileId; // id in PhysicalTexture (range in [0, 63])
        public int ActiveTime;
    }

    public class PageTable
    {
        private LruList m_lruList;

        private int m_tileNum;

        public PageTable(int mipCount, int tileNum)
        {
            m_tileNum = tileNum;
            m_lruList = new LruList(m_tileNum);
        }

        public void Clear()
        {
            m_lruList = new LruList(m_tileNum);
        }

        public bool IsActive(uint pageId, out int activeFrame)
        {
            activeFrame = m_lruList.GetActiveFrame(pageId);
            return activeFrame != -1;
        }

        public void Refresh(uint pageId, int time)
        {
            m_lruList.Refresh(pageId, time);
        }

        public void SetActive(uint pageId, int time)
        {
            m_lruList.Add(pageId, time);
        }

        public uint GetTileId(uint pageId)
        {
            return m_lruList.GetTileId(pageId);
        }
    }

    internal class LruList
    {
        internal class Node
        {
            public PageItem Item;
            public Node Prev;
            public Node Next;
        }

        private Dictionary<uint, Node> m_HashTable;
        private Queue<uint> m_AvailableTiles;
        private Node m_Head;
        private Node m_Tail;
        private int m_Size;

        public LruList(int maxNum)
        {
            m_Size = 0;
            m_HashTable = new Dictionary<uint, Node>();
            m_AvailableTiles = new Queue<uint>();
            for (uint i = 0; i < maxNum * maxNum; i++)
            {
                m_AvailableTiles.Enqueue(i);
            }
        }

        public int GetActiveFrame(uint pageId)
        {
            if (m_HashTable.TryGetValue(pageId, out var node))
            {
                return node.Item.ActiveTime;
            }

            return -1;
        }

        public uint GetTileId(uint pageId)
        {
            if (m_HashTable.TryGetValue(pageId, out var value))
            {
                return value.Item.TileId;
            }

            return 0xffffffff;
        }

        public void Add(uint pageId, int time)
        {
            if (m_AvailableTiles.Count == 0)
            {
                // unmap the first node 
                uint removeId = m_Head.Item.PageId;
                m_HashTable.Remove(removeId);
                m_HashTable[pageId] = m_Head;
                m_Head.Item.PageId = pageId;
                m_Head.Item.ActiveTime = time;

                // the first becomes the tail
                m_Head.Prev = m_Tail;
                m_Tail.Next = m_Head;
                m_Tail = m_Head;
                // the second becomes the first
                m_Head = m_Head.Next;
                m_Tail.Next = null;
                m_Head.Prev = null;
            }
            else
            {
                PageItem item = new PageItem()
                {
                    PageId = pageId,
                    TileId = m_AvailableTiles.Dequeue(),
                    ActiveTime = time
                };
                if (m_Size == 0)
                {
                    m_Tail = new Node()
                    {
                        Item = item,
                        Next = null,
                        Prev = null
                    };
                    m_Head = m_Tail;
                }
                else
                {
                    m_Tail.Next = new Node()
                    {
                        Item = item,
                        Next = null,
                        Prev = m_Tail
                    };
                    m_Tail = m_Tail.Next;
                }

                m_Size++;
                m_HashTable[pageId] = m_Tail;
            }
        }

        public void Refresh(uint pageId, int time)
        {
            Node node = m_HashTable[pageId];
            node.Item.ActiveTime = time;
            if (node == m_Tail)
            {
                return;
            }

            if (node == m_Head)
            {
                m_Head = m_Head.Next;
                m_Head.Prev = null;
            }
            else
            {
                node.Next.Prev = node.Prev;
                node.Prev.Next = node.Next;
            }

            node.Next = null;
            m_Tail.Next = node;
            node.Prev = m_Tail;
            m_Tail = node;
        }
    }
}