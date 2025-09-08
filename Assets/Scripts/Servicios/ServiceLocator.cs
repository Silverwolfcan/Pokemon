using System;
using System.Collections.Generic;

/// Servicio simple de localización. Evita dependencias duras entre UI y lógica.
/// No usa namespaces para integrarse sin fricción con el proyecto actual.
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

    public static void Reset() => _services.Clear();

    public static void Register<T>(T instance) where T : class
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        _services[typeof(T)] = instance;
    }

    public static T Get<T>() where T : class
    {
        _services.TryGetValue(typeof(T), out var obj);
        return obj as T;
    }

    public static bool TryGet<T>(out T instance) where T : class
    {
        instance = Get<T>();
        return instance != null;
    }
}
