﻿namespace BrandUp.Commands
{
    public interface ICommand
    {

    }

    public interface ICommand<out TResult> : ICommand
    {
    }

    
}