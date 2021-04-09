/*******************************************************************
 * StaticCoroutine.cs
 * 
 * Use this class from a non-monobehaviour derived class to give it access to coroutines!
 * https://forum.unity.com/threads/passing-in-a-monobehaviour-to-run-a-coroutine.588049/
 * 
 * Call StaticCoroutine.Start( YourRoutine() );
 * 
 ********************************************************************/
using System.Collections;
using UnityEngine;

public class StaticCoroutine
{
    private static StaticCoroutineRunner runner;

    public static Coroutine Start( IEnumerator coroutine )
    {
        if( coroutine == null )
        {
            Debug.LogError( "StaticCoroutine.cs Start() ERROR: coroutine sent in is null!" );
            return null;
        }

        EnsureRunner();
        return runner.StartCoroutine( coroutine );
    }

    private static void EnsureRunner()
    {
        if( runner == null )
        {
            runner = new GameObject( "[Static Coroutine Runner]" ).AddComponent<StaticCoroutineRunner>();
            Object.DontDestroyOnLoad( runner.gameObject );
        }
    }

    private class StaticCoroutineRunner: MonoBehaviour
    {
    }

} //END class