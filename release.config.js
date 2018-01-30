module.exports = {
    verifyConditions: [
        '@semantic-release/changelog',
        '@semantic-release/git',
        '@semantic-release/github',
    ],
    publish: [
        '@semantic-release/changelog',
        '@semantic-release/git',
        {
            path: '@semantic-release/github',
            assets: 'artifacts/**/*.nupkg',
        },
    ],
}
