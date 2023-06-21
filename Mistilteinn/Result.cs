#region Copyright (c) Mistilteinn/Mistilteinn
// GPL v3 License
// 
// Mistilteinn/Mistilteinn
// Copyright (c) 2023 Mistilteinn/Result.cs
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

public interface IResult<out T, TError>
{
    public static IResult<T, TError> Unit(T value) => new Success<T, TError>(value);

    public static IResult<T, TError> Failure(TError reason) => new Failure<T, TError>(reason);

    public bool IsDefined => this is Success<T, TError>;

    public T Get()
    {
        return this switch
        {
            Success<T, TError>(var value) => value,
            Failure<T, TError>(var error) => error is { } err 
                ? throw new IndexOutOfRangeException(err.ToString())
                : throw new IndexOutOfRangeException("IResult<T, TError> is Failure"),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public IResult<U, TError> Select<U>(Func<T, U> func)
    {
        return this switch
        {
            Success<T, TError>(var value) => new Success<U, TError>(func(value)),
            Failure<T, TError>(var error) => new Failure<U, TError>(error),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public IResult<U, TError> SelectMany<U>(Func<T, IResult<U, TError>> func)
    {
        return this switch
        {
            Success<T, TError>(var value) => func(value),
            Failure<T, TError>(var error) => new Failure<U, TError>(error),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public IResult<T, UError> SelectError<UError>(Func<TError, UError> func)
    {
        return this switch
        {
            Success<T, TError>(var value) => new Success<T, UError>(value),
            Failure<T, TError>(var error) => new Failure<T, UError>(func(error)),
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

    public TResult Fold<TResult>(Func<T, TResult> caseSome, Func<TError, TResult> caseNone)
    {
        return this switch
        {
            Success<T, TError>(var value) => caseSome(value),
            Failure<T, TError>(var error) => caseNone(error),
            _ => throw new NotImplementedException()
        };
    }

    public IOption<T> ToOption()
    {
        return Fold(IOption<T>.Unit, _ => IOption<T>.None());
    }
}

public sealed record Success<T, TError>(T Value) : IResult<T, TError>;

public sealed record Failure<T, TError>(TError Error) : IResult<T, TError>;