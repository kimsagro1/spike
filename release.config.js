module.exports = {
    verifyConditions: [
        '@semantic-release/changelog',
        '@semantic-release/github',
    ],
    publish: [
        '@semantic-release/changelog',
        {
            path: '@semantic-release/github',
            assets: 'artifacts/**/*.nupkg',
        },
    ],
}
