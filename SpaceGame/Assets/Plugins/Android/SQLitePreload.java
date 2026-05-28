package com.mikecook.starcaptain;

/**
 * Loaded by Unity's Android player at startup.
 * Calling load() forces the JVM class loader — which has access to the
 * app's private lib directory — to open libsqlite3.so before IL2CPP's
 * DllImport machinery attempts dlopen("sqlite3").  Once the JVM has
 * loaded the library it is resident in the process; IL2CPP's subsequent
 * dlopen call finds it immediately via the linker cache.
 */
public class SQLitePreload {
    public static void load() {
        System.loadLibrary("sqlite3");
    }
}
