using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A specialized list type for disk-backed Unity assets.
///
/// This class behaves the same as an ordinary List<T>,
/// with the exception that whenever the caller assigns or
/// appends a list item, the list item will first be
/// serialized to disk. (The method used for serialization
/// is specified be passing a function to the constructor.)
///
/// This class allows me to use lists of disk-backed assets and in-memory
/// assets to be used interchangeably. In particular, it removes
/// the need for a lot of specialized code in EditorGltfImporter for
/// dealing with serialized Unity assets.
/// </summary>
/// <typeparam name="T"></typeparam>
public class SerializedAssetList<T> : IList<T> where T : class
{
    /// <summary>
    /// The wrapped List<T> instance that provides most of
    /// the functionality for this class.
    /// </summary>
    protected List<T> _list;
    
    /// <summary>
    /// A function that serializes a list item (T)
    /// to disk and then returns an updated reference to
    /// that list item.
    /// </summary>
    protected Func<int, T, T> _serializer;
    
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="serializer">
    /// Serializer is a function that takes a list
    /// item of type T as input, serializes the item
    /// to disk, then returns an updated reference to the
    /// list item.
    /// </param>
    public SerializedAssetList(Func<int, T, T> serializer)
    {
        _list = new List<T>();
        _serializer = serializer;
    }
    
    /// <summary>
    /// Get a typed enumerator over the list items, where
    /// the enumerator returns items of type T.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    /// <summary>
    /// Get a untyped enumerator over the list items, where
    /// the enumerator returns items of type `object`.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    /// <summary>
    /// Append an item to the end of the list.
    /// </summary>
    public void Add(T item)
    {
        if (item != null)
            item = _serializer(_list.Count, item);
        _list.Add(item);
    }

    /// <summary>
    /// Remove all items from the list.
    /// </summary>
    public void Clear()
    {
        _list.Clear();
    }

    /// <summary>
    /// Return true if the list contains the given item,
    /// or false otherwise.
    /// </summary>
    public bool Contains(T item)
    {
        return _list.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public bool Remove(T item)
    {
        throw new NotImplementedException();
    }

    public int Count { get => _list.Count; }
    public bool IsReadOnly { get => ((IList<T>)_list).IsReadOnly; }
   
    public int IndexOf(T item)
    {
        throw new NotImplementedException();
    }

    public void Insert(int index, T item)
    {
        throw new NotImplementedException();
    }

    public void RemoveAt(int index)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Assign an item to the given list index.
    /// </summary>
    public T this[int index]
    {
        get => _list[index];
        set
        {
            if (value != null)
                _list[index] = _serializer(index, value);
            else
                _list[index] = null;
        }
    }
}
