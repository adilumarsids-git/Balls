mergeInto(LibraryManager.library, {
  SolanaConnect: function (gameObjectNamePtr) {
    const gameObjectName = UTF8ToString(gameObjectNamePtr);
    if (!window.solana || !window.solana.isPhantom) {
      SendMessage(gameObjectName, "OnWalletError", "Phantom wallet not available.");
      return;
    }
    window.solana.connect()
      .then(function (response) {
        if (response && response.publicKey) {
          SendMessage(gameObjectName, "OnWalletConnected", response.publicKey.toString());
        } else {
          SendMessage(gameObjectName, "OnWalletError", "Wallet connection failed.");
        }
      })
      .catch(function (err) {
        SendMessage(gameObjectName, "OnWalletError", err.message || "Wallet connection error.");
      });
  }
});
