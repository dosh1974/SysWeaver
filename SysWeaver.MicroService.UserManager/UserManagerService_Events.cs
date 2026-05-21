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

        ValueTask RaiseOnUserCreated(long id, String name)
        {
            OnUserCreated.RaiseEvents(id, name);
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

        ValueTask RaiseOnUserDeleted(long id, String name)
        {
            OnUserDeleted.RaiseEvents(id, name);
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

        ValueTask RaiseOnEmailAdded(long id, String email)
        {
            OnEmailAdded.RaiseEvents(id, email);
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

        ValueTask RaiseOnPhoneAdded(long id, String phone)
        {
            OnPhoneAdded.RaiseEvents(id, phone);
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

        ValueTask RaiseOnEmailChanged(long id, String newEmail, String oldEmail)
        {
            OnEmailChanged.RaiseEvents(id, newEmail, oldEmail);
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

        ValueTask RaiseOnPhoneChanged(long id, String newPhone, String oldPhone)
        {
            OnPhoneChanged.RaiseEvents(id, newPhone, oldPhone);
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

        ValueTask RaiseOnEmailRemoved(long id, String email)
        {
            OnEmailRemoved.RaiseEvents(id, email);
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

        ValueTask RaiseOnPhoneRemoved(long id, String phone)
        {
            OnPhoneRemoved.RaiseEvents(id, phone);
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

        ValueTask RaiseOnUserNameChanged(long id, String nickName)
        {
            OnUserNameChanged.RaiseEvents(id, nickName);
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

        ValueTask RaiseOnNickChanged(long id, String nickName)
        {
            OnNickChanged.RaiseEvents(id, nickName);
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

        ValueTask RaiseOnLanguageChanged(long id, String languageCode)
        {
            OnLanguageChanged.RaiseEvents(id, languageCode);
            return OnLanguageChangedAsync.RaiseEvents(id, languageCode);
        }

    }
}
