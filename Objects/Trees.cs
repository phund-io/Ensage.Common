﻿namespace Ensage.Common.Objects
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The trees.
    /// </summary>
    public class Trees
    {
        #region Static Fields

        /// <summary>
        ///     The all.
        /// </summary>
        private static List<Tree> all;

        /// <summary>
        ///     The loaded.
        /// </summary>
        private static bool loaded;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes static members of the <see cref="Trees" /> class.
        /// </summary>
        static Trees()
        {
            all = ObjectManager.GetEntities<Tree>().ToList();
            Events.OnLoad += (sender, args) =>
                {
                    if (loaded)
                    {
                        return;
                    }

                    Load();
                };
            if (!loaded && ObjectManager.LocalHero != null && Game.IsInGame)
            {
                Load();
            }

            Events.OnClose += (sender, args) =>
                {
                    ObjectManager.OnRemoveEntity -= ObjectMgr_OnRemoveEntity;
                    loaded = false;
                };
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     The get trees.
        /// </summary>
        /// <returns>
        ///     The <see cref="List" />.
        /// </returns>
        public static List<Tree> GetTrees()
        {
            return all;
        }

        #endregion

        #region Methods

        /// <summary>
        ///     The load.
        /// </summary>
        private static void Load()
        {
            all = ObjectManager.GetEntities<Tree>().ToList();
            ObjectManager.OnRemoveEntity += ObjectMgr_OnRemoveEntity;
            loaded = true;
        }

        /// <summary>
        ///     The object manager on remove entity.
        /// </summary>
        /// <param name="args">
        ///     The args.
        /// </param>
        private static void ObjectMgr_OnRemoveEntity(EntityEventArgs args)
        {
            var tree = args.Entity as Tree;
            if (tree != null)
            {
                all.Remove(tree);
            }
        }

        #endregion
    }
}