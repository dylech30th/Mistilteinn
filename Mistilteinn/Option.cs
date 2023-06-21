#region Copyright (c) Mistilteinn/Mistilteinn
// GPL v3 License
// 
// Mistilteinn/Mistilteinn
// Copyright (c) 2023 Mistilteinn/Option.cs
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

namespace Mistilteinn;

public interface IOption<out T>
{
    public static IOption<T> Unit(T value) => new Some<T>(value);

    public static IOption<T> None() => new None<T>();

    public bool IsDefined => this is Some<T>;

    public T Get()
    {
        return this switch
        {
            Some<T>(var value) => value,
            None<T> => throw new IndexOutOfRangeException("IOption<T> is None"),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public IOption<U> Select<U>(Func<T, U> func)
    {
        return this switch
        {
            Some<T>(var value) => new Some<U>(func(value)),
            None<T> => new None<U>(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public IOption<U> SelectMany<U>(Func<T, IOption<U>> func)
    {
        return this switch
        {
            Some<T>(var value) => func(value),
            None<T> => new None<U>(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    public void ForEach(Action<T> action)
    {
        if (IsDefined)
        {
            action(Get());
        }
    }

    public bool All(Func<T, bool> predicate)
    {
        return !IsDefined || predicate(Get());
    }

    public bool Any(Func<T, bool> predicate)
    {
        return IsDefined && predicate(Get());
    }

    public TResult Fold<TResult>(Func<T, TResult> caseSome, Func<TResult> caseNone)
    {
        return this switch
        {
            Some<T>(var value) => caseSome(value),
            None<T> => caseNone(),
            _ => throw new NotImplementedException()
        };
    }

    public IResult<T, TError> ToResult<TError>(TError error)
    {
        return Fold(IResult<T, TError>.Unit, () => IResult<T, TError>.Failure(error));
    }
}

public sealed record Some<T>(T Value) : IOption<T>;

public sealed record None<T> : IOption<T>;