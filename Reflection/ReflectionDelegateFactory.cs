using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace SkadiNet
{
    internal static class ReflectionDelegateFactory
    {
        internal static Func<object, object> BoxedFieldGetter(FieldInfo field)
        {
            try
            {
                if (field == null) return null;
                ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
                Expression target = field.IsStatic ? null : Expression.Convert(instance, field.DeclaringType);
                Expression body = Expression.Convert(Expression.Field(target, field), typeof(object));
                return Expression.Lambda<Func<object, object>>(body, instance).Compile();
            }
            catch { return null; }
        }

        internal static Action<object, object> BoxedFieldSetter(FieldInfo field)
        {
            try
            {
                if (field == null || field.IsInitOnly) return null;
                ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
                ParameterExpression value = Expression.Parameter(typeof(object), "value");
                Expression target = field.IsStatic ? null : Expression.Convert(instance, field.DeclaringType);
                Expression assign = Expression.Assign(Expression.Field(target, field), Expression.Convert(value, field.FieldType));
                return Expression.Lambda<Action<object, object>>(assign, instance, value).Compile();
            }
            catch { return null; }
        }

        internal static Func<object, object> BoxedInstanceMethod(MethodInfo method)
        {
            try
            {
                if (method == null || method.ContainsGenericParameters || method.GetParameters().Length != 0)
                    return null;

                ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
                Expression target = method.IsStatic ? null : Expression.Convert(instance, method.DeclaringType);
                MethodCallExpression call = Expression.Call(target, method);
                Expression body = method.ReturnType == typeof(void)
                    ? Expression.Block(call, Expression.Constant(null))
                    : Expression.Convert(call, typeof(object));
                return Expression.Lambda<Func<object, object>>(body, instance).Compile();
            }
            catch { return null; }
        }

        internal static Action<object, object> InstanceAction1(MethodInfo method)
        {
            try
            {
                if (method == null || method.ContainsGenericParameters) return null;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1) return null;

                ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
                ParameterExpression arg = Expression.Parameter(typeof(object), "arg");
                Expression target = method.IsStatic ? null : Expression.Convert(instance, method.DeclaringType);
                MethodCallExpression call = Expression.Call(target, method, Expression.Convert(arg, parameters[0].ParameterType));
                Expression body = method.ReturnType == typeof(void) ? (Expression)call : Expression.Block(call, Expression.Empty());
                return Expression.Lambda<Action<object, object>>(body, instance, arg).Compile();
            }
            catch { return null; }
        }

        internal static Func<object, Vector3> Vector3FieldGetter(FieldInfo field)
        {
            try
            {
                if (field == null || field.FieldType != typeof(Vector3)) return null;
                ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
                Expression target = field.IsStatic ? null : Expression.Convert(instance, field.DeclaringType);
                Expression body = Expression.Field(target, field);
                return Expression.Lambda<Func<object, Vector3>>(body, instance).Compile();
            }
            catch { return null; }
        }

        internal static Func<object, Vector3> Vector3InstanceMethod(MethodInfo method)
        {
            try
            {
                if (method == null || method.ContainsGenericParameters || method.ReturnType != typeof(Vector3) || method.GetParameters().Length != 0)
                    return null;

                ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
                Expression target = method.IsStatic ? null : Expression.Convert(instance, method.DeclaringType);
                return Expression.Lambda<Func<object, Vector3>>(Expression.Call(target, method), instance).Compile();
            }
            catch { return null; }
        }

        internal static Func<object, Quaternion> QuaternionFieldGetter(FieldInfo field)
        {
            try
            {
                if (field == null || field.FieldType != typeof(Quaternion)) return null;
                ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
                Expression target = field.IsStatic ? null : Expression.Convert(instance, field.DeclaringType);
                Expression body = Expression.Field(target, field);
                return Expression.Lambda<Func<object, Quaternion>>(body, instance).Compile();
            }
            catch { return null; }
        }

        internal static Func<object, Quaternion> QuaternionInstanceMethod(MethodInfo method)
        {
            try
            {
                if (method == null || method.ContainsGenericParameters || method.ReturnType != typeof(Quaternion) || method.GetParameters().Length != 0)
                    return null;

                ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
                Expression target = method.IsStatic ? null : Expression.Convert(instance, method.DeclaringType);
                return Expression.Lambda<Func<object, Quaternion>>(Expression.Call(target, method), instance).Compile();
            }
            catch { return null; }
        }
    }
}
