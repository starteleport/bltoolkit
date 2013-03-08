﻿#region

using System;
using System.Collections.Generic;
using BLToolkit.Data;
using BLToolkit.DataAccess;
using BLToolkit.Emit;
using BLToolkit.Reflection;
using Castle.DynamicProxy;

#endregion

namespace BLToolkit.Mapping
{
    public class FullObjectMapper : ObjectMapper, IObjectMapper
    {
        private readonly DbManager _db;
        private readonly ProxyGenerator _proxy;

        public FullObjectMapper(DbManager db)
        {
            _db = db;
            _proxy = new ProxyGenerator();
            PropertiesMapping = new List<IMapper>();
        }

        #region IPropertiesMapping Members

        public List<IMapper> PropertiesMapping { get; private set; }
        public IPropertiesMapping ParentMapping { get; set; }

        #endregion

        public bool IsNullable { get; set; }
        public bool ColParent { get; set; }

        #region IMapper Members

        public int DataReaderIndex { get; set; }
        public SetHandler Setter { get; set; }
        public Type PropertyType { get; set; }
        public string PropertyName { get; set; }
        public AssociationAttribute Association { get; set; }

        #endregion

        #region IObjectMapper

        public bool IsLazy { get; set; }
        public bool ContainsLazyChild { get; set; }
        public GetHandler Getter { get; set; }

        #endregion

        #region ILazyMapper

        public GetHandler ParentKeyGetter { get; set; }
        public GetHandler PrimaryKeyValueGetter { get; set; }

        #endregion

        public override void Init(MappingSchema mappingSchema, Type type)
        {
            // TODO implement this method
            base.Init(mappingSchema, type);
        }

        public override object CreateInstance()
        {
            object result = ContainsLazyChild
                                ? _proxy.CreateClassProxy(PropertyType, new LazyValueLoadInterceptor(this, LoadLazy))
                                : FunctionFactory.Remote.CreateInstance(PropertyType);

            return result;
        }

        public override object CreateInstance(InitContext context)
        {
            return CreateInstance();
        }

        private object LoadLazy(IMapper mapper, object proxy, Type parentType)
        {
            var lazyMapper = (ILazyMapper) mapper;
            object key = lazyMapper.ParentKeyGetter(proxy);

            var fullSqlQuery = new FullSqlQuery(_db, ignoreLazyLoad: true);
            object parentLoadFull = fullSqlQuery.SelectByKey(parentType, key);
            if (parentLoadFull == null)
            {
                object value = Activator.CreateInstance(mapper is CollectionFullObjectMapper
                                                            ? (mapper as CollectionFullObjectMapper).PropertyCollectionType
                                                            : mapper.PropertyType);
                return value;
            }

            var objectMapper = (IObjectMapper) mapper;
            return objectMapper.Getter(parentLoadFull);
        }
    }
}