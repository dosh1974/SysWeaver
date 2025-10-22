using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Db;
using SysWeaver.Net;
using SimpleStack.Orm;
using SysWeaver.Data;
using SysWeaver.MicroService.Db;
using SimpleStack.Orm.Expressions.Statements.Typed;
using SysWeaver.IsoData;

namespace SysWeaver.MicroService
{
    public sealed partial class UserManagerService
    {
		/// <summary>
		/// Arguments are: id, name
		/// </summary>
		public event Action<long, String> OnUserCreated;

        /// <summary>
        /// Arguments are: id, name
        /// </summary>
        public event Func<long, String, Task> OnUserCreatedAsync;

        Task RaiseOnUserCreated(long id, String name)
        {
            OnUserCreated?.Invoke(id, name);
            return OnUserCreatedAsync.RaiseEvents(id, name);
        }

        /// <summary>
        /// Arguments are: id, name
        /// </summary>
        public event Action<long, String> OnUserDeleted;

        /// <summary>
        /// Arguments are: id, name
        /// </summary>
        public event Func<long, String, Task> OnUserDeletedAsync;

        Task RaiseOnUserDeleted(long id, String name)
        {
            OnUserDeleted?.Invoke(id, name);
            return OnUserDeletedAsync.RaiseEvents(id, name);
        }


        /// <summary>
        /// Arguments are: id, email
        /// </summary>
        public event Action<long, String> OnEmailAdded;

        /// <summary>
        /// Arguments are: id, email
        /// </summary>
        public event Func<long, String, Task> OnEmailAddedAsync;

        Task RaiseOnEmailAdded(long id, String email)
        {
            OnEmailAdded?.Invoke(id, email);
            return OnEmailAddedAsync.RaiseEvents(id, email);
        }

        /// <summary>
        /// Arguments are: id, phone
        /// </summary>
        public event Action<long, String> OnPhoneAdded;

        /// <summary>
        /// Arguments are: id, phone
        /// </summary>
        public event Func<long, String, Task> OnPhoneAddedAsync;

        Task RaiseOnPhoneAdded(long id, String phone)
        {
            OnPhoneAdded?.Invoke(id, phone);
            return OnPhoneAddedAsync.RaiseEvents(id, phone);
        }

        /// <summary>
        /// Arguments are: id, new email, old email
        /// </summary>
        public event Action<long, String, String> OnEmailChanged;

        /// <summary>
        /// Arguments are: id, new email, old email
        /// </summary>
        public event Func<long, String, String, Task> OnEmailChangedAsync;

        Task RaiseOnEmailChanged(long id, String newEmail, String oldEmail)
        {
            OnEmailChanged?.Invoke(id, newEmail, oldEmail);
            return OnEmailChangedAsync.RaiseEvents(id, newEmail, oldEmail);
        }

        /// <summary>
        /// Arguments are: id, new phone, old phone
        /// </summary>
        public event Action<long, String, String> OnPhoneChanged;

        /// <summary>
        /// Arguments are: id, new phone, old phone
        /// </summary>
        public event Func<long, String, String, Task> OnPhoneChangedAsync;

        Task RaiseOnPhoneChanged(long id, String newPhone, String oldPhone)
        {
            OnPhoneChanged?.Invoke(id, newPhone, oldPhone);
            return OnPhoneChangedAsync.RaiseEvents(id, newPhone, oldPhone);
        }

        /// <summary>
        /// Arguments are: id, email
        /// </summary>
        public event Action<long, String> OnEmailRemoved;

        /// <summary>
        /// Arguments are: id, email
        /// </summary>
        public event Func<long, String, Task> OnEmailRemovedAsync;

        Task RaiseOnEmailRemoved(long id, String email)
        {
            OnEmailRemoved?.Invoke(id, email);
            return OnEmailRemovedAsync.RaiseEvents(id, email);
        }

        /// <summary>
        /// Arguments are: id, phone
        /// </summary>
        public event Action<long, String> OnPhoneRemoved;

        /// <summary>
        /// Arguments are: id, phone
        /// </summary>
        public event Func<long, String, Task> OnPhoneRemovedAsync;

        Task RaiseOnPhoneRemoved(long id, String phone)
        {
            OnPhoneRemoved?.Invoke(id, phone);
            return OnPhoneRemovedAsync.RaiseEvents(id, phone);
        }



        /// <summary>
        /// Arguments are: id, user name
        /// </summary>
        public event Action<long, String> OnUserNameChanged;

        /// <summary>
        /// Arguments are: id, user name
        /// </summary>
        public event Func<long, String, Task> OnUserNameChangedAsync;

        Task RaiseOnUserNameChanged(long id, String nickName)
        {
            OnUserNameChanged?.Invoke(id, nickName);
            return OnUserNameChangedAsync.RaiseEvents(id, nickName);
        }



        /// <summary>
        /// Arguments are: id, nick
        /// </summary>
        public event Action<long, String> OnNickChanged;

        /// <summary>
        /// Arguments are: id, nick
        /// </summary>
        public event Func<long, String, Task> OnNickChangedAsync;

        Task RaiseOnNickChanged(long id, String nickName)
        {
            OnNickChanged?.Invoke(id, nickName);
            return OnNickChangedAsync.RaiseEvents(id, nickName);
        }


        /// <summary>
        /// Arguments are: id, nick
        /// </summary>
        public event Action<long, String> OnLanguageChanged;

        /// <summary>
        /// Arguments are: id, nick
        /// </summary>
        public event Func<long, String, Task> OnLanguageChangedAsync;

        Task RaiseOnLanguageChanged(long id, String languageCode)
        {
            OnLanguageChanged?.Invoke(id, languageCode);
            return OnLanguageChangedAsync.RaiseEvents(id, languageCode);
        }

    }
}
