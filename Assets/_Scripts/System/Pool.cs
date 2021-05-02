using System;
using System.Collections.Generic;

public class Pool<T>
{
    Func<T> acquire;
    Action<T> init;
    Action<T> release;

    Queue<T> available = new Queue<T>();

    public Pool(Func<T> acquire, Action<T> init, Action<T> release)
    {
        this.acquire = acquire;
        this.init = init;
        this.release = release;
    }

    public T Aquire()
    {
        T obj;
        if (available.Count > 0)
        {
            obj = available.Dequeue();
        }
        else
        {
            obj = acquire.Invoke();
        }

        init.Invoke(obj);
        return obj;
    }

    public void Release(T obj)
    {
        release.Invoke(obj);
        if (!available.Contains(obj))
        {
            available.Enqueue(obj);
        }
    }
}
