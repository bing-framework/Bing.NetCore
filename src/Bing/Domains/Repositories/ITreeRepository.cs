using System;
using System.Collections.Generic;
using System.Text;
using Bing.Domains.Entities.Trees;

namespace Bing.Domains.Repositories
{
    public interface ITreeRepository<TEntity,in TKey,in TParentId>:IRepository<TEntity,TKey>,ITreeCompactRepository<TEntity,TKey,TParentId> where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>
    {
    }
}
