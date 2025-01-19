// <auto-generated/>
#pragma warning disable
using Microsoft.AspNetCore.Routing;
using System;
using System.Linq;
using Wolverine.Http;

namespace Internal.Generated.WolverineHandlers
{
    // START: DELETE_optional_result
    public class DELETE_optional_result : Wolverine.Http.HttpHandler
    {
        private readonly Wolverine.Http.WolverineHttpOptions _wolverineHttpOptions;

        public DELETE_optional_result(Wolverine.Http.WolverineHttpOptions wolverineHttpOptions) : base(wolverineHttpOptions)
        {
            _wolverineHttpOptions = wolverineHttpOptions;
        }



        public override async System.Threading.Tasks.Task Handle(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            // Reading the request body via JSON deserialization
            var (cmd, jsonContinue) = await ReadJsonAsync<WolverineWebApi.Validation.BlockUser2>(httpContext);
            if (jsonContinue == Wolverine.HandlerContinuation.Stop) return;
            var user = WolverineWebApi.Validation.ValidatedCompoundEndpoint2.Load(cmd);
            var result1 = WolverineWebApi.Validation.ValidatedCompoundEndpoint2.Validate(user);
            // Evaluate whether or not the execution should be stopped based on the IResult value
            if (result1 != null && !(result1 is Wolverine.Http.WolverineContinue))
            {
                await result1.ExecuteAsync(httpContext).ConfigureAwait(false);
                return;
            }


            
            // The actual HTTP request handler execution
            var result_of_Handle = WolverineWebApi.Validation.ValidatedCompoundEndpoint2.Handle(cmd, user);

            await WriteString(httpContext, result_of_Handle);
        }

    }

    // END: DELETE_optional_result
    
    
}

