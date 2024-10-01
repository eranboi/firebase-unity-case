const functions = require("firebase-functions");

exports.getServerTimestamp = functions.https.onCall((data, context) => {
  return {
    timestamp: Date.now(),
  };
});
