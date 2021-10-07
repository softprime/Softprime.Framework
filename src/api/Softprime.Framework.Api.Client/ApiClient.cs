﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Softprime.Framework.Api.Client.Helpers;
using Softprime.Framework.Api.Dto;
using Softprime.Framework.Api.Dto.Default;

namespace Softprime.Framework.Api.Client
{
    /// <summary>
    /// Classe genérica para operações CRUD em WebAPI
    /// </summary>
    /// <typeparam name="T">Tipo do objeto a ser tratado</typeparam>
    /// <typeparam name="TId">Tipo do Id do objeto</typeparam>
    public class ApiClient<T, TId> : ApiClientBase, IApiClient<T, TId> where T : IDto<TId>
    {
        public ApiClient(string uriService, string baseUrl)
        : base(uriService, baseUrl)
        {
        }

        public ApiClient(string uriService, string baseUrl, string apiKey)
            : base(uriService, baseUrl, apiKey)
        {
        }

        public ApiClient(string uriService, string baseUrl, string apiKey, string tenantKey)
           : base(uriService, baseUrl, apiKey, tenantKey)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Retorma um IEnumerable do objeto tipado
        /// </summary>
        /// <returns>IEnumerable</returns>
        public async Task<IEnumerable<T>> Get()
        {
            return await Result<IEnumerable<T>>(await Client.GetAsync(UriService));
        }

        /// <summary>
        /// Busca os resultados com paginação
        /// </summary>
        /// <param name="page"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public async Task<IPagedResult<T>> Get(int page, int size)
        {
            return await Result<PagedResultDto<T>>(await Client.GetAsync($"{UriService}?page={(page < 0 ? 0 : page)}&size={(size < 1 ? 1 : size)}"));
        }

        /// <inheritdoc />
        /// <summary>
        /// Retorna um objeto de acordo com o id
        /// </summary>
        /// <param name="id">Id do objeto</param>
        /// <returns>Objeto</returns>
        public async Task<T> Get(TId id)
        {
            return await Result<T>(await Client.GetAsync($"{UriService}/{id}"));
        }

        /// <inheritdoc />
        /// <summary>
        /// Recebe e retorna o objeto tipado
        /// </summary>
        /// <param name="obj">Objeto a ser tratado</param>
        /// <returns>Objeto</returns>
        public async Task<T> Post(T obj)
        {
            return await Result<T>(await Client.PostAsJsonAsync($"{UriService}", obj));
        }

        /// <summary>
        /// Recebe e retorna o objeto tipado
        /// </summary>
        /// <param name="obj">Objeto a ser tratado</param>
        /// <returns>Objeto</returns>
        public async Task<T> Put(T obj)
        {
            return await Result<T>(await Client.PutAsJsonAsync($"{UriService}/{obj.Id}", obj));
        }

        /// <summary>
        /// Deleta o objeto identitificado pelo id
        /// </summary>
        /// <param name="id">Id do objeto</param>
        /// <returns>StatusCode da operação</returns>
        public async Task Delete(TId id)
        {
            var response = await Client.DeleteAsync($"{UriService}/{id}");

            if (!response.IsSuccessStatusCode)
                throw new ApiException(await response.Content.ReadAsStringAsync());
        }
    }
}
